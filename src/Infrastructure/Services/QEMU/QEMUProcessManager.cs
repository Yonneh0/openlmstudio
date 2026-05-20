using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.QEMU;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenLMStudio.Infrastructure.Services.QEMU;

/// <summary>
/// Manages QEMU virtual machine instances with QMP protocol support.
/// </summary>
public class QEMUProcessManager : IQEMUProcessManager, IDisposable
{
    private readonly ILogger<QEMUProcessManager> _logger;
    private readonly ConcurrentDictionary<string, VMInstance> _instances = new();
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private int _qmpPortBase = 9100;
    private int _monPortBase = 10000;

    private static readonly Dictionary<ArchitectureType, string[]> _archBinaries = new()
    {
        { ArchitectureType.X86_64, new[] { "qemu-system-x86_64" } },
        { ArchitectureType.I386, new[] { "qemu-system-i386" } },
        { ArchitectureType.AArch64, new[] { "qemu-system-aarch64" } },
        { ArchitectureType.ARMv7L, new[] { "qemu-system-arm" } },
        { ArchitectureType.RISC_V64, new[] { "qemu-system-riscv64" } },
        { ArchitectureType.RISC_V32, new[] { "qemu-system-riscv32" } },
        { ArchitectureType.AVR, new[] { "qemu-system-avr" } },
        { ArchitectureType.MIPS, new[] { "qemu-system-mips" } },
        { ArchitectureType.MIPS64, new[] { "qemu-system-mips64" } },
        { ArchitectureType.MIPSEL, new[] { "qemu-system-mipsel" } },
        { ArchitectureType.MIPS64EL, new[] { "qemu-system-mips64el" } },
        { ArchitectureType.PPC, new[] { "qemu-system-ppc" } },
        { ArchitectureType.PPC64, new[] { "qemu-system-ppc64" } },
        { ArchitectureType.PPCemb, new[] { "qemu-system-ppcemb" } },
        { ArchitectureType.SPARC, new[] { "qemu-system-sparc" } },
        { ArchitectureType.SPARC64, new[] { "qemu-system-sparc64" } },
    };

    public QEMUProcessManager(ILogger<QEMUProcessManager> logger)
    {
        _logger = logger;
    }

    public IEnumerable<VMInstance> Instances => _instances.Values;

    /// <summary>
    /// Gets the actual System.Diagnostics.Process for the specified VM.
    /// Only available in the Infrastructure layer.
    /// </summary>
    public Process? GetProcess(string vmId) => _processes.GetValueOrDefault(vmId);

    public async Task<VMInstance> CreateVMAsync(VMCreationConfig config)
    {
        var binary = GetBinary(config.Architecture);
        var args = BuildArgs(config);

        var qmpPort = _qmpPortBase + ExtractNumericId(config.Id);
        var monPort = _monPortBase + ExtractNumericId(config.Id);

        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = binary,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        var procId = proc?.Id.ToString() ?? string.Empty;
        var instance = new VMInstance(
            config.Id,
            config.Architecture,
            GetDefaultMachine(config.Architecture),
            config.Accelerator,
            procId,
            new QmpSocket("tcp", "localhost", qmpPort),
            new QmpSocket("unix", $"/tmp/openlmstudio-qemu-{config.Id}-monitor", null),
            VMRunState.Paused,
            config.CpuTopology,
            config.RamBytes,
            config.DiskImages,
            config.NetworkDevices,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _instances[config.Id] = instance;
        if (proc != null)
        {
            _processes[config.Id] = proc;
            RegisterProcessListeners(instance, proc);
        }
        _logger.LogInformation("Created VM {VmId} ({Architecture}) with binary {Binary}",
            config.Id, config.Architecture, binary);

        return instance;
    }

    public async Task StartVMAsync(string vmId)
    {
        if (!_instances.TryGetValue(vmId, out var vm) || vm.State != VMRunState.Paused)
            throw new InvalidOperationException($"VM {vmId} is paused or not found.");

        await ExecuteQMPCommandAsync(vmId, "cont").ConfigureAwait(false);
        UpdateState(vmId, VMRunState.Running);
    }

    public async Task PauseVMAsync(string vmId)
    {
        if (!_instances.TryGetValue(vmId, out var vm) || vm.State != VMRunState.Running)
            throw new InvalidOperationException($"VM {vmId} is not running.");

        await ExecuteQMPCommandAsync(vmId, "stop").ConfigureAwait(false);
        UpdateState(vmId, VMRunState.Paused);
    }

    public async Task ResumeVMAsync(string vmId)
    {
        if (!_instances.TryGetValue(vmId, out var vm) || vm.State != VMRunState.Paused)
            throw new InvalidOperationException($"VM {vmId} is not paused.");

        await ExecuteQMPCommandAsync(vmId, "cont").ConfigureAwait(false);
        UpdateState(vmId, VMRunState.Running);
    }

    public async Task StopVMAsync(string vmId)
    {
        if (!_instances.TryGetValue(vmId, out var vm))
            throw new InvalidOperationException($"VM {vmId} not found.");

        await ExecuteQMPCommandAsync(vmId, "system_powerdown").ConfigureAwait(false);
        UpdateState(vmId, VMRunState.ShuttingDown);
    }

    public async Task DeleteVMAsync(string vmId)
    {
        if (_processes.TryRemove(vmId, out var proc))
        {
            try { proc.Kill(); } catch { }
        }
        if (_instances.TryRemove(vmId, out var vm))
        {
            foreach (var disk in vm.DiskImages)
                try { File.Delete(disk.File); } catch { }
        }
    }

    // Per QMP spec, capability negotiation is per-connection, not per-command.
    // Each ExecuteQMPCommandAsync opens a new connection, so negotiation happens once per call.
    public async Task<object?> ExecuteQMPCommandAsync(string vmId, string command, Dictionary<string, object?>? args = null)
    {
        if (!_instances.TryGetValue(vmId, out var vm))
            throw new InvalidOperationException($"VM {vmId} not found.");

        using var client = new TcpClient();
        await client.ConnectAsync(vm.QmpSocket.Address!, vm.QmpSocket.Port!.Value).ConfigureAwait(false);
        await using var ns = client.GetStream();
        using var reader = new StreamReader(ns);
        using var writer = new StreamWriter(ns) { AutoFlush = true };

        // Capability negotiation handshake (once per connection)
        await SendQMPMessageAsync(writer, "qmp_capabilities").ConfigureAwait(false);
        var capResponse = await ReadQMPResponseAsync(reader).ConfigureAwait(false);
        if (capResponse is JsonObject capObj && !capObj.ContainsKey("return") && !capObj.ContainsKey("error"))
        {
            // First response might be welcome banner; read until we get the capability response
            await ReadQMPResponseAsync(reader).ConfigureAwait(false);
        }

        // Execute command
        if (args != null)
            await SendQMPMessageAsync(writer, command, args).ConfigureAwait(false);
        else
            await SendQMPMessageAsync(writer, command).ConfigureAwait(false);

        return await ReadQMPResponseAsync(reader).ConfigureAwait(false);
    }

    public async Task<List<object>> QueryBlockDevicesAsync(string vmId)
    {
        var result = await ExecuteQMPCommandAsync(vmId, "query-block").ConfigureAwait(false);
        return result is JsonObject obj
            ? obj["devices"]?.AsArray()?.Select(d => (object)d!).ToList() ?? new List<object>()
            : new List<object>();
    }

    public void Dispose()
    {
        foreach (var proc in _processes.Values)
        {
            try { proc.Kill(); } catch { }
        }
        _processes.Clear();
        _instances.Clear();
    }

    // ---- Helpers ----

    private static string GetBinary(ArchitectureType arch) =>
        _archBinaries.GetValueOrDefault(arch, Array.Empty<string>())[0];

    private static string GetDefaultMachine(ArchitectureType arch)
    {
        return arch switch
        {
            ArchitectureType.X86_64 => "q35",
            ArchitectureType.AArch64 => "virt",
            ArchitectureType.RISC_V64 => "microvm",
            ArchitectureType.AVR => "",
            _ => "default"
        };
    }

    private static int ExtractNumericId(string id)
    {
        var digits = new string(id.Where(char.IsDigit).ToArray());
        return digits.Length > 0 ? int.Parse(digits) : 0;
    }

    private string BuildArgs(VMCreationConfig config)
    {
        var sb = new StringBuilder();
        var arch = config.Architecture;
        var machine = GetDefaultMachine(arch);

        if (!string.IsNullOrEmpty(machine))
            sb.Append($"-machine {machine} ");

        sb.Append($"-smp {config.CpuTopology.Sockets ?? 1}x{config.CpuTopology.Cores}x{config.CpuTopology.Threads} ");
        sb.Append($"-m {config.RamBytes / (1024 * 1024)} ");

        if (config.Accelerator == AcceleratorType.KVM)
            sb.Append("-enable-kvm ");

        foreach (var disk in config.DiskImages)
        {
            sb.Append($"-drive file={disk.File},if=virtio,format={disk.Format},media={disk.Media} ");
        }

        for (int i = 0; i < config.NetworkDevices.Count; i++)
        {
            var net = config.NetworkDevices[i];
            sb.Append($"-netdev {net.BackendType},id=net{i} ");
            sb.Append($"-device virtio-net-pci,netdev=net{i}{(net.MacAddress != null ? $",macaddr={net.MacAddress}" : "")} ");
        }

        if (arch == ArchitectureType.AVR)
        {
            // AVR: no machine flag, no disk images — runs from flash
            sb.Clear();
            sb.Append("-bios ");
            if (config.DiskImages.Count > 0 && !string.IsNullOrEmpty(config.DiskImages[0].File))
                sb.Append(config.DiskImages[0].File);
        }

        if (arch == ArchitectureType.AArch64 || arch == ArchitectureType.RISC_V64)
        {
            // ARM/RISC-V: EDK2 UEFI firmware
            var edk2Dir = Environment.GetEnvironmentVariable("EDK2_DIR");
            if (!string.IsNullOrEmpty(edk2Dir))
                sb.Append($"-bios {edk2Dir}/Firmware.bin ");
        }

        return sb.ToString().Trim();
    }

    private void RegisterProcessListeners(VMInstance vm, Process proc)
    {
        proc.Exited += (sender, args) =>
        {
            UpdateState(vm.Id, VMRunState.Shutdown);
            _logger.LogInformation("VM {VmId} exited.", vm.Id);
        };
    }

    private void UpdateState(string vmId, VMRunState newState)
    {
        if (_instances.TryGetValue(vmId, out var vm))
        {
            vm.State = newState;
            vm.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task SendQMPMessageAsync(TextWriter writer, string command, Dictionary<string, object?>? args = null)
    {
        var obj = new JsonObject { ["execute"] = command };
        if (args != null)
        {
            var entries = args
                .Where(kv => kv.Value != null)
                .Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, JsonNode.Parse(JsonSerializer.Serialize(kv.Value!))))
                .ToList();
            obj["arguments"] = new JsonObject(entries);
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(obj)).ConfigureAwait(false);
    }

    private static async Task<object?> ReadQMPResponseAsync(TextReader reader)
    {
        var line = await reader.ReadLineAsync().ConfigureAwait(false);
        return line != null ? JsonSerializer.Deserialize<JsonObject>(line) : null;
    }
}