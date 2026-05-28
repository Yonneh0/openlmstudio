using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Orchestrates System AI with QEMU VMs for cross-architecture workflows.
/// </summary>
public class SystemAICoordinator : ISystemAICoordinator
{
    private readonly ISystemAIClient _systemAI;
    private readonly IQEMUProcessManager _qemuManager;
    private readonly IArchPromptService _archPromptService;
    private readonly IToolchainRegistry _toolchainRegistry;
    private readonly ILogger<SystemAICoordinator> _logger;

    public SystemAICoordinator(
        ISystemAIClient systemAI,
        IQEMUProcessManager qemuManager,
        IArchPromptService archPromptService,
        IToolchainRegistry toolchainRegistry,
        ILogger<SystemAICoordinator> logger)
    {
        _systemAI = systemAI;
        _qemuManager = qemuManager;
        _archPromptService = archPromptService;
        _toolchainRegistry = toolchainRegistry;
        _logger = logger;
    }

    public async Task<WorkflowResult> HandleCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new WorkflowResult(false, "Empty command", null, "Command cannot be empty");

        var intent = ParseIntent(command);

        return intent switch
        {
            "bugFixing" => await ExecuteBugFixingWorkflowAsync(command),
            "crossCompile" => await ExecuteCrossCompilationWorkflowAsync(command),
            _ => await ExecuteDefaultWorkflowAsync(command)
        };
    }

    public async Task<string?> GetOrCreateArchVMAsync(ArchitectureType arch)
    {
        // Find existing running VM for this architecture
        var existing = _qemuManager.Instances
            .Where(v => v.Architecture == arch && v.State == VMRunState.Running)
            .FirstOrDefault();

        if (existing != null)
        {
            _logger.LogInformation("Found existing running VM for {Architecture}: {VmId}", arch, existing.Id);
            return existing.Id;
        }

        // Create a new VM for this architecture
        _logger.LogInformation("Creating new VM for {Architecture}", arch);
        var config = new VMCreationConfig(
            Id: $"arch-{arch.ToString().ToLower()}",
            Architecture: arch,
            Accelerator: AcceleratorType.TCG,
            CpuTopology: new CpuTopology(1, null, null, 2, 1),
            RamBytes: 2L * 1024 * 1024 * 1024, // 2GB
            DiskImages: new List<DiskImageConfig>(),
            NetworkDevices: new List<NetworkDeviceConfig>());

        var vm = await _qemuManager.CreateVMAsync(config);
        return vm.Id;
    }

    private static string ParseIntent(string command)
    {
        var lower = command.ToLowerInvariant();

        if (lower.Contains("bug") || lower.Contains("fix") || lower.Contains("compile") || lower.Contains("build"))
            return "bugFixing";

        if (lower.Contains("cross") || lower.Contains("compile") || lower.Contains("target") || lower.Contains("arch"))
            return "crossCompile";

        return "default";
    }

    private async Task<WorkflowResult> ExecuteBugFixingWorkflowAsync(string command)
    {
        var workflow = new Workflow(
            "bugFixing",
            "Compile and test code in architecture VM, report errors",
            new[] { "Detect architecture", "Compile in VM", "Run tests", "Report results" });

        // Determine target architecture from command
        var targetArch = DetermineTargetArchitecture(command);
        var vm = await GetOrCreateArchVMAsync(targetArch);

        if (vm == null)
            return new WorkflowResult(false, "Failed to create VM", null, "Could not create architecture VM");

        // Get the toolchain for the architecture
        var compiler = await _toolchainRegistry.GetToolchainAsync(targetArch, "gcc");
        if (string.IsNullOrEmpty(compiler))
            return new WorkflowResult(false, "Toolchain not available", null, $"Could not get toolchain for {targetArch}");

        // Build the system prompt for this architecture
        var systemPrompt = _archPromptService.GetSystemPrompt(targetArch);

        // Send the bug fixing request to System AI with architecture context
        var prompt = $"""
            {systemPrompt}

            Bug Fixing Workflow:
            Target Architecture: {targetArch}
            Compiler: {compiler}
            Command: {command}

            Please analyze the code and suggest fixes for compilation errors.
            """;

        var response = await _systemAI.SendMessageAsync(prompt);

        return new WorkflowResult(true, $"Bug fixing workflow completed for {targetArch}", response, null);
    }

    private async Task<WorkflowResult> ExecuteCrossCompilationWorkflowAsync(string command)
    {
        var workflow = new Workflow(
            "crossCompile",
            "Cross-compile from source architecture to target architecture",
            new[] { "Parse source and target architectures", "Set up toolchains", "Cross-compile", "Verify output" });

        // Parse architectures from command (e.g., "cross compile x86_64 to aarch64")
        var (sourceArch, targetArch) = ParseArchitectures(command);

        var sourceVm = await GetOrCreateArchVMAsync(sourceArch);
        if (sourceVm == null)
            return new WorkflowResult(false, "Failed to create source VM", null, "Could not create source architecture VM");

        var targetVm = await GetOrCreateArchVMAsync(targetArch);
        if (targetVm == null)
            return new WorkflowResult(false, "Failed to create target VM", null, "Could not create target architecture VM");

        // Get cross-compile environment variables
        var envVars = _archPromptService.GetCrossCompileEnvVars(targetArch);

        // Get the cross-compiler toolchain
        var crossCompiler = await _toolchainRegistry.GetToolchainAsync(targetArch, "gcc");

        var prompt = $"""
            Cross-Compilation Workflow:
            Source Architecture: {sourceArch}
            Target Architecture: {targetArch}
            Cross-Compiler: {crossCompiler}
            Environment Variables: {envVars}
            Command: {command}

            Please provide the cross-compilation commands and verify the build output.
            """;

        var response = await _systemAI.SendMessageAsync(prompt);

        return new WorkflowResult(true, $"Cross-compilation workflow completed: {sourceArch} -> {targetArch}", response, null);
    }

    private async Task<WorkflowResult> ExecuteDefaultWorkflowAsync(string command)
    {
        var workflow = new Workflow(
            "default",
            "General System AI processing",
            new[] { "Parse command", "Process with System AI", "Return result" });

        var response = await _systemAI.SendMessageAsync(command);

        return new WorkflowResult(true, "Command processed successfully", response, null);
    }

    private static ArchitectureType DetermineTargetArchitecture(string command)
    {
        var lower = command.ToLowerInvariant();

        if (lower.Contains("aarch64") || lower.Contains("arm64"))
            return ArchitectureType.AArch64;
        if (lower.Contains("riscv") || lower.Contains("risc-v"))
            return ArchitectureType.RISC_V64;
        if (lower.Contains("avr"))
            return ArchitectureType.AVR;
        if (lower.Contains("mips"))
            return ArchitectureType.MIPS;
        if (lower.Contains("ppc") || lower.Contains("powerpc"))
            return ArchitectureType.PPC;

        return ArchitectureType.X86_64;
    }

    private static (ArchitectureType Source, ArchitectureType Target) ParseArchitectures(string command)
    {
        var lower = command.ToLowerInvariant();

        ArchitectureType source = ArchitectureType.X86_64;
        ArchitectureType target = ArchitectureType.AArch64;

        if (lower.Contains("x86") || lower.Contains("x64"))
            source = ArchitectureType.X86_64;
        if (lower.Contains("aarch64") || lower.Contains("arm64"))
            target = ArchitectureType.AArch64;
        if (lower.Contains("riscv") || lower.Contains("risc-v"))
            target = ArchitectureType.RISC_V64;

        return (source, target);
    }
}
