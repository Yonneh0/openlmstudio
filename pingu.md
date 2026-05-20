# Pingu (System AI) + QEMU Tooling — Implementation Plan

## Overview

Adding **Pingu** (the System AI mascot/avatar), **System AI** (llama.cpp inference), and **QEMU tooling** (cross-architecture virtualization) to OpenLMStudio.

## Existing Architecture (OpenLMStudio)

```
OpenLMStudio/
├── src/Domain/                    # Models + Interfaces (DDD)
│   ├── Interfaces/
│   │   ├── IChatService.cs
│   │   ├── IModelService.cs
│   │   ├── IPluginRegistry.cs
│   │   └── ISandbox.cs
│   └── Models/
│       ├── Chat.cs, Message.cs, ChatContext.cs
│       ├── ModelType.cs (TextGeneration, ImageGeneration, Diffusion, ...)
│       ├── ModelMetadata.cs, ModelLoadState.cs
│       └── ...
├── src/Application/               # Application-layer interfaces
│   └── Interfaces/
│       ├── IChatCompletionService.cs
│       ├── IConversationManager.cs
│       ├── IServerService.cs
│       ├── IModelRepository.cs
│       ├── IChatContextManager.cs
│       └── ...
├── src/Infrastructure/            # Concrete services
│   ├── DependencyInjection.cs     # IServiceCollection extensions
│   └── Services/
│       ├── ChatService.cs
│       ├── ConversationManager.cs
│       ├── ServerService.cs
│       ├── ModelManager.cs
│       ├── LlamaCppChatCompletionService.cs
│       ├── ContextWindowBudgeter.cs
│       ├── ConversationContextCompressor.cs
│       ├── McpClient.cs, McpToolCaller.cs, ...
│       ├── DiffusionPipelineService.cs
│       └── ...
├── src/Desktop/                   # Avalonia UI (.axaml + .axaml.cs)
│   ├── MainWindow.axaml(.cs)
│   ├── SettingsWindow.axaml(.cs)
│   └── App.axaml(.cs)
└── tests/                         # Unit tests
```

### Key Patterns Observed

| Pattern | Usage |
|---------|-------|
| **Layered DDD** | Domain (POCOs + interfaces) → Application (interfaces) → Infrastructure (concrete) → Desktop (UI) |
| **DI Registration** | `DependencyInjection.cs` adds services to `IServiceCollection` |
| **UI** | Avalonia `.axaml` (XAML) + `.axaml.cs` (code-behind), `Dispatcher.UIThread.InvokeAsync()` |
| **Async** | `async Task` with `ConfigureAwait(false)` for domain logic; `async void` only for event handlers |
| **Concurrency** | `ConcurrentDictionary` for thread-safe state sharing |
| **Logging** | `ILogger<T>` via `Microsoft.Extensions.Logging` |
| **Error handling** | Try/catch with logging, graceful degradation |
| **Configuration** | `ServerConfiguration` POCOs, passed to services |
| **Events** | `event Action<T>` on services (e.g., `StateChanged`) |

---

## 1. Pingu Store — Reactive State

Port `pinguStore.ts` state machine to Domain models.

```
src/Domain/Models/
├── Pingu/
│   ├── PinguState.cs           // Core reactive state
│   ├── PinguMood.cs            // Idle, Thinking, Speaking, Happy, Error, Working
│   ├── PinguPanelType.cs       // Skills, Settings, Models, Compile, Logs, About, None
│   ├── AwakeningPhase.cs       // None, Shake, Stretch, Glow
│   ├── PinguAvatarConfig.cs    // Animation settings (bob speed, blink interval, etc.)
│   └── PinguEvent.cs           // Event-based updates (like Zustand's notify)

src/Application/Interfaces/
└── IPinguStore.cs              // Interface for Pingu state management
```

### PinguState.cs

```csharp
// src/Domain/Models/Pingu/PinguState.cs
namespace OpenLMStudio.Domain.Models.Pingu;

public enum PinguMood { Idle, Thinking, Speaking, Happy, Error, Working }
public enum PinguPanelType { Skills, Settings, Models, Compile, Logs, About, None }
public enum AwakeningPhase { None, Shake, Stretch, Glow }

public class PinguState
{
    public PinguMood Mood { get; set; }
    public bool IsVisible { get; set; }
    public bool IsMenuOpen { get; set; }
    public PinguPanelType ActivePanel { get; set; }
    public bool IsBlinking { get; set; }
    public int MouthFrame { get; set; }
    public double BobSpeed { get; set; }

    // Awakening state
    public bool IsAwake { get; set; }
    public bool HasGguf { get; set; }
    public bool HasLlamaCpp { get; set; }
    public AwakeningPhase AwakeningPhase { get; set; }
    public bool IsLoadingModel { get; set; }
    public double LoadProgress { get; set; }
}

// Event-based updates — mirrors PinguStore's notify mechanism
public class PinguStateChangedEventArgs : EventArgs
{
    public PinguState State { get; set; }
    public PinguStateChangedEventArgs(PinguState state) => State = state;
}
```

### PinguStore (Application Layer)

```csharp
// src/Application/Interfaces/IPinguStore.cs
namespace OpenLMStudio.Application.Interfaces;

public interface IPinguStore
{
    PinguState State { get; }
    event EventHandler<PinguStateChangedEventArgs>? OnStateChanged;

    Task UpdateMoodAsync(PinguMood mood);
    Task ToggleMenuAsync();
    Task SetActivePanelAsync(PinguPanelType panel);
    Task SetAwakeAsync(bool awake);
    Task SetLoadingProgressAsync(double progress);
    Task SetBlinkStateAsync(bool blinking);
}
```

```csharp
// src/Infrastructure/Services/PinguStore.cs
namespace OpenLMStudio.Infrastructure.Services;

public class PinguStore : IPinguStore
{
    private readonly PinguState _state = new();
    public PinguState State => _state;

    public event EventHandler<PinguStateChangedEventArgs>? OnStateChanged;

    private void NotifyChanged()
    {
        OnStateChanged?.Invoke(this, new PinguStateChangedEventArgs(_state));
    }

    public Task UpdateMoodAsync(PinguMood mood)
    {
        _state.Mood = mood;
        NotifyChanged();
        return Task.CompletedTask;
    }

    // ... other mutation methods
}
```

### DI Registration

Add to `src/Infrastructure/DependencyInjection.cs`:

```csharp
services.AddSingleton<IPinguStore, PinguStore>();
```

---

## 2. Pingu Avatar — Avalonia UI Component

Port the SVG-based avatar with CSS animations to Avalonia.

```
src/Desktop/Controls/
├── PinguAvatar.axaml
├── PinguAvatar.axaml.cs
├── PinguPanel.axaml
├── PinguPanel.axaml.cs
└── PinguHomeTile.axaml
```

### PinguAvatar.axaml.cs (code-behind)

```csharp
// src/Desktop/Controls/PinguAvatar.axaml.cs
namespace OpenLMStudio.Desktop.Controls;

public partial class PinguAvatar : UserControl
{
    private readonly IPinguStore _pingu;

    public PinguAvatar(IPinguStore pingu)
    {
        _pingu = pingu;
        InitializeComponent();
        _pingu.OnStateChanged += OnPinguStateChanged;
    }

    private void OnPinguStateChanged(object? sender, PinguStateChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateAvatarDisplay(e.State);
            UpdateAnimationClass(e.State);
        });
    }

    private void UpdateAvatarDisplay(PinguState state)
    {
        // Select eye/mouth SVGs based on mood (from PinguAvatar.tsx lines 102-131)
        // Update bob animation class based on state.BobSpeed
        // Update glow class based on mood == Thinking/Working
    }

    private void UpdateAnimationClass(PinguState state)
    {
        // Classes: animate-pulse-slow, animate-bob-{speed}, animate-jump-50
        // Translate to Avalonia's animation system (Storyboard/DoubleAnimation)
    }
}
```

### PinguPanel.axaml (overlay panel)

```xml
<!-- src/Desktop/Controls/PinguPanel.axaml -->
<UserControl x:Class="OpenLMStudio.Desktop.Controls.PinguPanel">
    <Border x:Name="PanelBorder" ...>
        <!-- Skills tab, Settings tab, Models tab, Compile tab, Logs tab, About tab -->
        <!-- Port from PinguPanel.tsx -->
    </Border>
</UserControl>
```

### PinguHomeTile.axaml (pre-awaken tile)

```xml
<!-- src/Desktop/Controls/PinguHomeTile.axaml -->
<!-- Port from PenguinHomeTile.tsx — tile displayed before Pingu is awake -->
```

---

## 3. System AI Client — Port of systemAI.ts

Port the llama.cpp System AI client to C#.

```
src/Domain/Models/
└── SystemAI/
    ├── SystemAIConfig.cs
    ├── SystemAIResponse.cs

src/Application/Interfaces/
└── ISystemAIClient.cs

src/Infrastructure/Services/
└── SystemAIClient.cs
```

### SystemAIClient.cs

```csharp
// src/Infrastructure/Services/SystemAIClient.cs
namespace OpenLMStudio.Infrastructure.Services;

public class SystemAIClient : ISystemAIClient
{
    private Process? _process;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SystemAIClient> _logger;
    private readonly string _modelPath;
    private readonly int _port; // 8081

    public event EventHandler<SseChunk>? OnChunk;
    public event EventHandler<SseDone>? OnDone;
    public event EventHandler<string>? OnError;

    public async Task<bool> StartAsync()
    {
        // Same pattern as LlamaCppChatCompletionService: spawn llama-server
        // --mlock -m "{_modelPath}" --port 8081
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "llama-server",
            Arguments = $"--mlock -m \"{_modelPath}\" --port {_port}",
            UseShellExecute = false
        });
        _process = proc;
        return await WaitForServerReady(_port).ConfigureAwait(false);
    }

    public async Task<string?> SendMessageAsync(string message, string? compressedContext = null)
    {
        // HTTP POST to llama-server (port 8081) — same as sendHttpPostRequest
        var body = new ChatRequest
        {
            Messages = new[] { new ChatMessage { Role = "user", Content = message } },
            Stream = true,
            Temperature = 0.3f,
            TopP = 0.9f
        };

        var response = await _httpClient.PostAsync(
            $"http://127.0.0.1:{_port}/v1/chat/completions",
            new JsonContent(body)
        );

        // Parse SSE stream — same logic as systemAI.ts
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line?.StartsWith("data: ") == true)
            {
                var data = line.Substring(6);
                if (data == "[DONE]")
                {
                    OnDone?.Invoke(this, new SseDone());
                    break;
                }
                var parsed = JsonDocument.Parse(data).RootElement;
                if (parsed["choices"]?.GetProperty(0)["delta"]?["content"]?.GetString() is string content)
                    OnChunk?.Invoke(this, new SseChunk(content));
            }
        }
        return null;
    }

    public async Task StopAsync()
    {
        if (_process == null) return;
        var proc = _process;
        _process = null;
        try { proc.Kill(); } catch { }
        await Task.Delay(2000);
        if (!proc.Killed) proc.Kill();
    }
}
```

### DI Registration

```csharp
services.AddSingleton<ISystemAIClient, SystemAIClient>();
```

---

## 3b. System AI — Key Implementation Details (from OpenLLMCode)

### stdin/stdout Protocol (NOT HTTP)

The TypeScript `src/engine/systemAI.ts` uses **llama-server stdin/stdout protocol**, not HTTP streaming:

```typescript
// TypeScript stdin/stdout protocol (from systemAI.ts lines 81-91)
spawnFn('llama-server', [
  '--mlock',
  '-m', modelPath,
  '--port', '8081',
]);
// Sends JSON to stdin, reads from stdout until 'done' appears
```

### C# Equivalent (Process + Pipe)

```csharp
public class SystemAIClient : ISystemAIClient
{
    private Process? _process;

    public async Task<string?> SendMessageAsync(string message)
    {
        // Use Process.StandardInput/StandardOutput
        _process!.StandardInput.WriteLine(JSON.stringify(new {
            type = "message",
            system_prompt = GetSystemPrompt(),
            message
        }));

        // Read response from stdout
        var response = await Task<string>.Run(() =>
        {
            var sb = new StringBuilder();
            string? line;
            while ((line = _process.StandardOutput.ReadLine()) != null)
            {
                sb.AppendLine(line);
                if (line.Contains("done")) break;
            }
            return sb.ToString().Trim();
        });
        return response;
    }
}
```

### Compile Scripts (getCompileScript, getInstallCompilerCommand)

```csharp
public static class CompilationHelper
{
    public static string GetCompileScript(CompilationConfig config)
    {
        // Generates OS-specific CMake build commands
        // win32: cmake -B build -DGGML_XNNPACK=ON && cmake --build build --config Release
        // darwin: cmake -B build \\ -DGGML_XNNPACK=ON \\ && cmake --build build --config Release
        // linux: cmake -B build \\ -DGGML_XNNPACK=ON \\ && cmake --build build --config Release
    }

    public static string GetInstallCompilerCommand(string os)
    {
        return os switch
        {
            "win32" => "winget install --id Microsoft.VisualStudio.2022.BuildTools",
            "darwin" => "xcode-select --install",
            _ => "sudo apt install build-essential cmake"
        };
    }

    public static async Task<(bool hasGit, bool hasCMake, bool hasCompiler)> CheckPrerequisites(string os)
    {
        // Check for git, cmake, compiler availability
    }
}
```

---

## 3c. Engine Manager Features (from OpenLLMCode)

```
src/engine/manager.ts
```

### Hardware Detection (detectHardware)

```typescript
// Port of detectHardware() — detects GPU via wmic, RAM via platform-specific API
export async function detectHardware(): Promise<{
  platform: string;
  gpu?: string;
  ramGB: number;
}>;
```

### Backend Recommendation (getRecommendedBackend)

```typescript
// Port of getRecommendedBackend() — metal for macOS, cuda for NVIDIA, vulkan fallback, cpu default
export function getRecommendedBackend(hardware): Backend;
```

### GitHub Binary Download (downloadForBackend)

```typescript
// Port of downloadForBackend() — fetches llama.cpp binaries from GitHub releases
export async function downloadForBackend(backend: Backend): Promise<string>;
```

### Config Persistence

```typescript
// Port of loadConfig/saveConfig — reads/writes config.json in AppData
export function loadConfig(): EngineConfig;
export function saveConfig(cfg: EngineConfig): void;
```

### App Update Checking

```typescript
// Port of checkForAppUpdates() — checks for app updates hourly via GitHub API
export async function checkForAppUpdates(): Promise<{ available: boolean; version: string; notes?: string } | null>;
```

---

## 3d. Pingu Store Events (from OpenLLMCode)

The TypeScript store (`pinguStore.ts`) uses **custom events** for cross-component communication:

```typescript
// Events dispatched from PinguStore
window.dispatchEvent(new CustomEvent('pingu-chat-open'));
window.dispatchEvent(new CustomEvent('pingu-chat-close'));
window.dispatchEvent(new CustomEvent('pingu-awakened'));
```

### Awakening Sequence (from pinguStore.ts)

```typescript
// Port of startAwakeningSequence()
// Phase 1: Shake for 0.5s → Phase 2: Stretch for 2s → Phase 3: Glow for 2s
// Dispatches 'pingu-awakened' event on completion
startAwakeningSequence() {
  set({ awakeningPhase: 'shake' });
  setTimeout(() => {
    set({ awakeningPhase: 'stretch' });
    setTimeout(() => {
      set({ awakeningPhase: 'glow' });
      setTimeout(() => {
        window.dispatchEvent(new CustomEvent('pingu-awakened'));
      }, 2000);
    }, 2000);
  }, 500);
}
```

### Blink Timer (from pinguStore.ts)

```typescript
// Port of startBlinkTimer() — random 1.5s to 3s intervals
export function startBlinkTimer() {
  const scheduleNextBlink = () => {
    const nextBlinkIn = Math.random() * (3000 - 1500) + 1500;
    blinkInterval = setTimeout(() => {
      usePinguStore.getState().triggerBlink();
      scheduleNextBlink();
    }, nextBlinkIn);
  };
  scheduleNextBlink();
}
```

### Convenience Functions (from pinguStore.ts)

```typescript
// Port these convenience functions
export function startSpeaking(): void;     // Sets mood to 'speaking', starts mouth animation loop
export function startThinking(): void;     // Sets mood to 'thinking'
export function completeTask(): void;      // Sets mood to 'happy' for 2s, plays "Noot noot!" sound
export function handleTaskError(): void;   // Sets mood to 'error' for 5s
export function startWorking(): void;      // Sets mood to 'working', bob speed 1.5x
export function idle(): void;              // Sets mood to 'idle'
```

### Pin/Unpin (from pinguStore.ts)

```typescript
// Port of pinAndOpenChat / unpinPingu
export function pinAndOpenChat(): void;  // Pins Pingu in place, opens chat dialog
export function unpinPingu(): void;      // Unpins and closes chat
```

---

## 4. QEMU Process Manager — C# Implementation

Port `processManager.ts` (~519 lines) to C# following the existing patterns.

```
src/Domain/Models/
└── QEMU/
    ├── QEMUTypes.cs           // All enums + records (Architecture, VM, Disk, etc.)
    ├── VMInstance.cs
    ├── VMCreationConfig.cs
    └── QmpCommandResult.cs

src/Application/Interfaces/
└── IQEMUProcessManager.cs

src/Infrastructure/Services/
└── QEMU/
    ├── QEMUProcessManager.cs
    └── QMPClient.cs
```

### QEMUTypes.cs (Domain layer)

```csharp
// src/Domain/Models/QEMU/QEMUTypes.cs
namespace OpenLMStudio.Domain.Models.QEMU;

public enum ArchitectureType
{
    X86_64, I386, AArch64, ARMv7L, RISC-V64, RISC-V32, AVR,
    MIPS, MIPS64, MIPSEL, MIPS64EL, PPC, PPC64, PPCemb, SPARC, SPARC64
}

public enum AcceleratorType { KVM, TCG }
public enum DiskFormatType { Raw, Qcow2, QED, VDI, VHDX, VMDK }
public enum NetworkBackendType { User, Tap, Socket, VDE, Hubport }
public enum VMRunState { Running, Paused, Debug, ShuttingDown, ShutdownRequest }

public record CpuTopology(int? Sockets, int? Dies, int? Clusters, int Cores, int Threads);
public record DiskImageConfig(string Id, string Media, DiskFormatType Format, string File);
public record NetworkDeviceConfig(string Id, NetworkBackendType BackendType, string? MacAddress);
public record QmpSocket(string Type, string? Address, int? Port);

public record VMInstance(
    string Id,
    ArchitectureType Architecture,
    string Machine,
    AcceleratorType Accelerator,
    Process Process,
    QmpSocket QmpSocket,
    QmpSocket MonSocket,
    VMRunState State,
    CpuTopology CpuTopology,
    long RamBytes,
    List<DiskImageConfig> DiskImages,
    List<NetworkDeviceConfig> NetworkDevices,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record VMCreationConfig(
    string Id,
    ArchitectureType Architecture,
    AcceleratorType Accelerator,
    CpuTopology CpuTopology,
    long RamBytes,
    List<DiskImageConfig> DiskImages,
    List<NetworkDeviceConfig> NetworkDevices
);
```

### QEMUProcessManager.cs (Infrastructure layer)

```csharp
// src/Infrastructure/Services/QEMU/QEMUProcessManager.cs
namespace OpenLMStudio.Infrastructure.Services.QEMU;

public class QEMUProcessManager : IQEMUProcessManager
{
    private readonly Dictionary<string, VMInstance> _instances = new();
    private const int QMP_PORT_BASE = 9100;

    private static readonly Dictionary<ArchitectureType, string[]> _archBinaries = new()
    {
        { ArchitectureType.X86_64, new[] { "qemu-system-x86_64" } },
        { ArchitectureType.AArch64, new[] { "qemu-system-aarch64" } },
        { ArchitectureType.RISC-V64, new[] { "qemu-system-riscv64" } },
        // ... all 16 architectures
    };

    private static readonly Dictionary<ArchitectureType, string> _cpuModels = new()
    {
        { ArchitectureType.X86_64, "host" },
        { ArchitectureType.AArch64, "cortex-a72" },
        { ArchitectureType.RISC-V64, "rv64" },
        { ArchitectureType.AVR, "avr" },
        // ... per buildArgs() logic
    };

    private static readonly Dictionary<ArchitectureType, string> _defaultMachines = new()
    {
        { ArchitectureType.X86_64, "q35" },
        { ArchitectureType.AArch64, "virt" },
        { ArchitectureType.RISC-V64, "microvm" },
        { ArchitectureType.AVR, "" },
        // ... per getDefaultMachine() logic
    };

    public async Task<VMInstance> CreateVMAsync(VMCreationConfig config)
    {
        var args = BuildArgs(config);
        var binary = GetBinary(config.Architecture);
        var proc = Process.Start(new ProcessStartInfo { FileName = binary, Arguments = args, UseShellExecute = false });

        var instance = new VMInstance(
            config.Id, config.Architecture, GetDefaultMachine(config.Architecture),
            config.Accelerator, proc,
            new QmpSocket("tcp", "localhost", QMP_PORT_BASE + ExtractNumericId(config.Id)),
            new QmpSocket("unix", $"/tmp/openlmcode-qemu-{config.Id}-monitor", null),
            VMRunState.Paused, config.CpuTopology, config.RamBytes,
            config.DiskImages, config.NetworkDevices, DateTime.UtcNow, DateTime.UtcNow
        );

        _instances[config.Id] = instance;
        RegisterProcessListeners(instance);
        return instance;
    }

    // QMP command execution (JSON over TCP — same as TypeScript)
    public async Task<object?> ExecuteQMPCommandAsync(string vmId, string command, Dictionary<string, object?>? args = null)
    {
        var vm = _instances[vmId];
        using var client = new TcpClient();
        await client.ConnectAsync(vm.QmpSocket.Address!, vm.QmpSocket.Port!.Value);

        // Capability negotiation handshake (per QMP spec)
        await SendQMPMessageAsync(client, new { execute = "qmp_capabilities" });
        await SendQMPMessageAsync(client, new { execute = command, arguments = args });

        // Parse response — QMP uses JSON over TCP/Unix socket
        return await ParseQMPResponseAsync(client);
    }

    // ... startVM, pauseVM, resumeVM, stopVM, deleteVM, queryBlockDevices, etc.
}
```

### DI Registration

```csharp
services.AddSingleton<IQEMUProcessManager, QEMUProcessManager>();
```

---

## 5. QEMU VM Creation Wizard — Avalonia UI Component

Port `VMCreationWizard.tsx` (~1033 lines) to Avalonia.

```
src/Desktop/Windows/
└── VMCreationWizardWindow.axaml
└── VMCreationWizardWindow.axaml.cs

src/Desktop/Controls/
├── ArchitectureStep.axaml
├── HardwareStep.axaml
├── DiskImagesStep.axaml
├── NetworkStep.axaml
└── ReviewStep.axaml
```

### VMCreationWizardWindow.axaml.cs (code-behind)

```csharp
// src/Desktop/Windows/VMCreationWizardWindow.axaml.cs
namespace OpenLMStudio.Desktop.Windows;

public partial class VMCreationWizardWindow : Window
{
    private readonly IQEMUProcessManager _qemuManager;
    private int _step = 0;
    private VMCreationForm _form = new();

    public VMCreationWizardWindow(IQEMUProcessManager qemuManager)
    {
        _qemuManager = qemuManager;
        InitializeComponent();
        DataContext = this;
    }

    private async void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        var config = ToVMCreationConfig(_form);
        await _qemuManager.CreateVMAsync(config);
        Close();
    }

    private void NextStep() => _step = Math.Min(_step + 1, 4);
    private void PrevStep() => _step = Math.Max(_step - 1, 0);
}
```

### VMCreationForm (Domain model for wizard state)

```csharp
// src/Domain/Models/QEMU/VMCreationForm.cs
namespace OpenLMStudio.Domain.Models.QEMU;

public class VMCreationForm
{
    public string Name { get; set; } = "";
    public ArchitectureType Architecture { get; set; } = ArchitectureType.X86_64;
    public AcceleratorType Accelerator { get; set; } = AcceleratorType.KVM;
    public int CpuSockets { get; set; } = 1;
    public int CpuCores { get; set; } = 2;
    public int CpuThreads { get; set; } = 1;
    public int RamMB { get; set; } = 2048;
    public List<DiskImageEntry> DiskImages { get; set; } = new();
    public List<NetworkDeviceEntry> NetworkDevices { get; set; } = new();
}

public class DiskImageEntry
{
    public string Id { get; set; } = "";
    public string Media { get; set; } = "disk";
    public DiskFormatType Format { get; set; } = DiskFormatType.Qcow2;
    public string Path { get; set; } = "";
    public bool IsNew { get; set; }
}

public class NetworkDeviceEntry
{
    public string Id { get; set; } = "";
    public NetworkBackendType BackendType { get; set; } = NetworkBackendType.User;
    public string? MacAddress { get; set; }
}
```

---

## 6. Toolchain Registry — Port of toolchainRegistry.ts

```
src/Application/Interfaces/
└── IToolchainRegistry.cs

src/Infrastructure/Services/
└── ToolchainRegistry.cs
```

```csharp
// src/Infrastructure/Services/ToolchainRegistry.cs
namespace OpenLMStudio.Infrastructure.Services;

public class ToolchainRegistry : IToolchainRegistry
{
    private readonly Dictionary<(ArchitectureType, string), string> _toolchains = new();

    public async Task<string?> GetToolchainAsync(ArchitectureType arch, string toolName)
    {
        // Download + cache toolchain for architecture
        // Port from toolchainRegistry.ts: per-arch binaries (gcc, binutils, gdb, etc.)
        var key = (arch, toolName);
        if (_toolchains.TryGetValue(key, out var path))
            return path;

        var downloadUrl = GetToolchainUrl(arch, toolName);
        var localPath = await DownloadToolchainAsync(downloadUrl, arch, toolName);
        _toolchains[key] = localPath;
        return localPath;
    }
}
```

---

## 7. VM Console — xterm.js Terminal (via WebView or native)

```
src/Desktop/Controls/
└── VMConsole.axaml
└── VMConsole.axaml.cs
```

```csharp
// Option 1: Embedded WebView for xterm.js (same as original)
// Option 2: Avalonia-based terminal using Terminal.Gui (Linux) or Process + TextBox (Windows)
```

---

## 8. Engine Logging — Port of engineLogger.ts

```
src/Infrastructure/Services/
└── EngineLogger.cs
```

### Key Types (from engineLogger.ts)

```typescript
// Types from engineLogger.ts
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error';

export interface EngineLogEntry {
  id: string;
  timestamp: number;
  level: LogLevel;
  message: string;
  source: 'primary' | 'systemAI';
}

export interface EngineLoggingSession {
  engineId: string;
  isActive: boolean;
  logEntries: EngineLogEntry[];
  logFile?: string;
  maxEntries: number;
}
```

### C# Implementation

```csharp
// src/Infrastructure/Services/EngineLogger.cs
namespace OpenLMStudio.Infrastructure.Services;

public enum LogLevel { Trace, Debug, Info, Warn, Error }

public class EngineLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public EngineType Source { get; set; }
}

public class EngineLoggingSession
{
    public EngineType EngineId { get; set; }
    public bool IsActive { get; set; }
    public List<EngineLogEntry> LogEntries { get; set; } = new();
    public string? LogFile { get; set; }
    public int MaxEntries { get; set; } = 10000;
}

public class EngineLogger : IEngineLogger
{
    private EngineLoggingSession? _primarySession;
    private EngineLoggingSession? _systemAISession;
    private readonly EngineLoggerConfig _config;

    public EngineLogEntry AddLogEntry(EngineType engineId, LogLevel level, string message)
    {
        var entry = new EngineLogEntry { Id = $"log-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", ... };
        var session = GetSession(engineId)!;
        session.LogEntries.Add(entry);
        if (session.LogEntries.Count > session.MaxEntries)
            session.LogEntries.RemoveAt(0);

        if (_config.EnableDiskLogging && session.LogFile != null)
            File.AppendAllText(session.LogFile, $"[{entry.Timestamp}] [{level}] {message}\n");

        return entry;
    }

    // Port of handleEngineStdout/handleEngineStderr
    public void HandleEngineStdout(EngineType engineId, string data)
    {
        var lines = data.Split('\n').Where(l => l.Trim().Length > 0);
        foreach (var line in lines)
        {
            try
            {
                var parsed = JsonDocument.Parse(line);
                if (parsed.RootElement.GetProperty("choices")
                    .GetProperty(0)["delta"]?.GetProperty("content")?.GetString() is string content)
                    AddLogEntry(engineId, LogLevel.Trace, $"Token: {content}");
            }
            catch
            {
                AddLogEntry(engineId, LogLevel.Debug, $"Raw: {line}");
            }
        }
    }

    public void HandleEngineStderr(EngineType engineId, string data)
    {
        var trimmed = data.Trim();
        if (trimmed.Length > 0)
            AddLogEntry(engineId, LogLevel.Warn, $"stderr: {trimmed}");
    }

    // Log rotation — port of rotateLogFileIfNecessary
    private void RotateLogFileIfNecessary(string logFilePath)
    {
        var stats = new FileInfo(logFilePath).Length;
        if (stats >= _config.DiskLogRotationSizeMB * 1024 * 1024)
        {
            int rotationIndex = 1;
            while (File.Exists(logFilePath + $".{rotationIndex}"))
                rotationIndex++;
            File.Move(logFilePath, logFilePath + $".{rotationIndex}");
            File.WriteAllText(logFilePath, $"[Rotated] previous {stats / (1024*1024)} MB moved to .{rotationIndex}\n");
        }
    }
}
```

### DI Registration

```csharp
services.AddSingleton<IEngineLogger, EngineLogger>();
```

---

## 8b. Context Compression — Port of contextCompression.ts

### Key Detail: Uses System AI (1B CPU model)

The TypeScript `src/engine/contextCompression.ts` (~327 lines) uses the **System AI client** to generate conversation summaries. Port to C# using the System AI stdin/stdout protocol.

### ContextCompressionService.cs

```csharp
// src/Infrastructure/Services/ContextCompressionService.cs
namespace OpenLMStudio.Infrastructure.Services;

public class ContextCompressionService : IContextCompressionService
{
    private readonly ISystemAIClient _systemAI;
    private readonly CompressionConfig _config;

    // Defaults from contextCompression.ts
    private const int DEFAULT_MAX_CONTEXT_TOKENS = 131072;
    private const int DEFAULT_MIN_ACTIVE_WINDOW_TOKENS = 2048;
    private const double DEFAULT_ACTIVE_WINDOW_PERCENTAGE = 0.15;
    private const double TOKENS_PER_CHAR = 0.25;

    public async Task<CompressedEntry[]> CompressConversationAsync(
        Message[] messages,
        CompressedEntry[] existingCompressedHistory,
        CompressionConfig? config = null)
    {
        var totalTokens = EstimateTokens(messages.Sum(m => m.Content?.Length ?? 0));

        if (totalTokens <= _config.MaxTotalContextTokens)
            return existingCompressedHistory;

        var (toCompress, keepActive) = SplitMessagesByTokens(messages, _config);
        if (toCompress.Count == 0)
            return existingCompressedHistory;

        var newEntry = await CompressMessagesAsync(existingCompressedHistory, toCompress);
        var updated = existingCompressedHistory.Concat(new[] { newEntry }).ToList();
        while (updated.Count > _config.MaxCompressedEntries)
            updated.RemoveAt(0);

        return updated.ToArray();
    }

    public (string? Preamble, Message[] ActiveMessages) GenerateFullContext(
        CompressedEntry[] compressedHistory,
        Message[] activeMessages)
    {
        var preamble = compressedHistory.Length > 0
            ? $"Earlier conversation summary:\n{string.Join("\n", compressedHistory.Select(e =>
                $"- {e.Summary}\n  Decisions: {string.Join("; ", e.KeyDecisions)}\n  Files modified: {string.Join(", ", e.FilesModified)}"))}"
            : null;

        return (preamble, activeMessages);
    }

    public CompressedStats GetCompressionStats(Message[] messages, CompressedEntry[] compressedHistory)
    {
        var totalChars = messages.Sum(m => m.Content?.Length ?? 0);
        var totalTokens = EstimateTokens(totalChars);
        var existingSummaryIds = compressedHistory.Select(e => e.Summary).ToList();
        var activeMessages = messages.Where(m => !existingSummaryIds.Contains(m.Content)).ToArray();
        var activeTokens = EstimateTokens(activeMessages.Sum(m => m.Content?.Length ?? 0));
        var compressedChars = compressedHistory.Sum(e => e.Summary.Length + e.KeyDecisions.Join(", ") + e.FilesModified.Join(", "));
        var compressionRatio = totalChars > 0 ? (int)((1 - compressedChars / totalChars) * 100) : 0;

        return new CompressedStats
        {
            TotalMessages = messages.Length,
            ActiveWindowSize = activeMessages.Length,
            CompressedEntriesCount = compressedHistory.Length,
            EstimatedActiveTokens = activeTokens,
            EstimatedCompressedTokens = EstimateTokens(compressedChars),
            CompressionRatio = compressionRatio
        };
    }

    private static int EstimateTokens(int charCount)
        => (int)Math.Ceiling(charCount * TOKENS_PER_CHAR);
}
```

### CompressedEntry (Domain model)

```csharp
// src/Domain/Models/ContextCompression/CompressedEntry.cs
namespace OpenLMStudio.Domain.Models.ContextCompression;

public class CompressedEntry
{
    public string Summary { get; set; } = "";
    public List<string> KeyDecisions { get; set; } = new();
    public List<string> FilesModified { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
```

---

## 8c. Engine Manager — Key Implementation Details (from OpenLLMCode)

### Hardware Detection (detectHardware)

```typescript
// Port of detectHardware() — detects GPU via wmic, RAM via platform-specific API
export async function detectHardware(): Promise<{
  platform: string;
  gpu?: string;
  ramGB: number;
}>;
```

```csharp
// src/Infrastructure/Services/HardwareDetector.cs
public class HardwareDetector
{
    public async Task<HardwareInfo> DetectAsync()
    {
        var platform = Environment.OSVersion.Platform == PlatformID.Win32Windows ? "win32" :
                        Environment.OSVersion.Platform == PlatformID.MacOSX ? "darwin" : "linux";

        string? gpu = null;
        if (platform == "win32")
        {
            // Port from detectHardware.ts: wmic path win32_VideoController get name
            gpu = ProcessCommand("wmic", "path win32_VideoController get name");
        }

        int ramGB = 16;
        if (platform == "win32")
            ramGB = ParseRamFromWmic();
        else if (platform == "darwin")
            ramGB = ParseRamFromSysctl();
        else
            ramGB = ParseRamFromProcMeminfo();

        return new HardwareInfo(platform, gpu, ramGB);
    }
}
```

### Backend Recommendation (getRecommendedBackend)

```typescript
// Port of getRecommendedBackend()
export function getRecommendedBackend(hardware): Backend {
  if (hardware.platform === 'darwin') return 'metal';
  if (hardware.gpu?.toLowerCase().includes('nvidia')) return 'cuda';
  if (hardware.gpu) return 'vulkan';
  return 'cpu';
}
```

### GitHub Binary Download (downloadForBackend)

```typescript
// Port of downloadForBackend() — fetches llama.cpp binaries from GitHub releases
export async function downloadForBackend(backend: Backend): Promise<string>;
```

### Config Persistence

```typescript
// Port of loadConfig/saveConfig — reads/writes config.json in AppData
export function loadConfig(): EngineConfig;
export function saveConfig(cfg: EngineConfig): void;
```

### App Update Checking

```typescript
// Port of checkForAppUpdates() — checks for app updates hourly via GitHub API
export async function checkForAppUpdates(): Promise<{ available: boolean; version: string; notes?: string } | null>;
```

---

## 8d. Pingu Store — Key Implementation Details (from OpenLLMCode)

### Events (from pinguStore.ts)

```typescript
// Events dispatched from PinguStore
window.dispatchEvent(new CustomEvent('pingu-chat-open'));
window.dispatchEvent(new CustomEvent('pingu-chat-close'));
window.dispatchEvent(new CustomEvent('pingu-awakened'));
```

### Awakening Sequence (from pinguStore.ts)

```typescript
// Port of startAwakeningSequence()
// Phase 1: Shake for 0.5s → Phase 2: Stretch for 2s → Phase 3: Glow for 2s
// Dispatches 'pingu-awakened' event on completion
startAwakeningSequence() {
  set({ awakeningPhase: 'shake' });
  setTimeout(() => {
    set({ awakeningPhase: 'stretch' });
    setTimeout(() => {
      set({ awakeningPhase: 'glow' });
      setTimeout(() => {
        window.dispatchEvent(new CustomEvent('pingu-awakened'));
      }, 2000);
    }, 2000);
  }, 500);
}
```

### Blink Timer (from pinguStore.ts)

```typescript
// Port of startBlinkTimer() — random 1.5s to 3s intervals
export function startBlinkTimer() {
  const scheduleNextBlink = () => {
    const nextBlinkIn = Math.random() * (3000 - 1500) + 1500;
    blinkInterval = setTimeout(() => {
      usePinguStore.getState().triggerBlink();
      scheduleNextBlink();
    }, nextBlinkIn);
  };
  scheduleNextBlink();
}
```

### Convenience Functions (from pinguStore.ts)

```typescript
// Port these convenience functions
export function startSpeaking(): void;     // Sets mood to 'speaking', starts mouth animation loop
export function startThinking(): void;     // Sets mood to 'thinking'
export function completeTask(): void;      // Sets mood to 'happy' for 2s, plays "Noot noot!" sound
export function handleTaskError(): void;   // Sets mood to 'error' for 5s
export function startWorking(): void;      // Sets mood to 'working', bob speed 1.5x
export function idle(): void;              // Sets mood to 'idle'
```

### Pin/Unpin (from pinguStore.ts)

```typescript
// Port of pinAndOpenChat / unpinPingu
export function pinAndOpenChat(): void;  // Pins Pingu in place, opens chat dialog
export function unpinPingu(): void;      // Unpins and closes chat
```

---

## 9. Architecture-Specific System Prompts — Port of archPrompts.ts

```
src/Infrastructure/Services/
└── QEMU/
    └── ArchPromptService.cs
```

```csharp
// src/Infrastructure/Services/QEMU/ArchPromptService.cs
namespace OpenLMStudio.Infrastructure.Services.QEMU;

public class ArchPromptService : IArchPromptService
{
    private static readonly Dictionary<ArchitectureType, string> _systemPrompts = new()
    {
        // x86_64: build/test commands, cross-compile vars (CC=gcc, CXX=g++, etc.),
        //         known issues (SMP locks, segfaults on AMD), disk format (raw)
        [ArchitectureType.X86_64] = "...",

        // ARM64: EDK2 UEFI firmware, virtio-net-pci, -bios EDK2 path
        [ArchitectureType.AArch64] = "...",

        // RISC-V: MicroVM, EDK2 UEFI, SBI firmware, no -bios for flash
        [ArchitectureType.RISC-V64] = "...",

        // AVR: bare-metal MCU, no machine flag, no disk, -bios firmware.hex
        [ArchitectureType.AVR] = "...",

        // MIPS/MIPS64/MIPSEL/MIPS64EL: Malta board, tcsh shell,
        //                              CMAKE_TARGET_ARCHITECTURE=mips/mips64/mipsel/mips64el
        [ArchitectureType.MIPS] = "...",
        [ArchitectureType.MIPS64] = "...",
        [ArchitectureType.MIPSEL] = "...",
        [ArchitectureType.MIPS64EL] = "...",

        // PowerPC: ppcemb (embedded), RSPS64, no SMP, -bios PReP firmware
        [ArchitectureType.PPC] = "...",
        [ArchitectureType.PPC64] = "...",

        // SPARC/SPARC64: SPARCstation 10/LEON3, GCC_SPARC,
        //                SPARC-specific known issues (floating point)
        [ArchitectureType.SPARC] = "...",
        [ArchitectureType.SPARC64] = "...",

        // i386: -bios SeaBIOS, -drive if=pflash,format=raw,...
        [ArchitectureType.I386] = "...",

        // ARMv7L: ARMv7L, virtio-net-pci, -bios EDK2
        [ArchitectureType.ARMv7L] = "...",
    };

    public string GetSystemPrompt(ArchitectureType arch) =>
        _systemPrompts.GetValueOrDefault(arch, "");

    public string GetCrossCompileEnvVars(ArchitectureType arch) =>
        _crossCompileEnv.GetValueOrDefault(arch, "");
}
```

---

## 10. Integration — Wiring into MainWindow

### MainWindow.axaml — Add PinguAvatar to bottom-right corner

```xml
<!-- src/Desktop/MainWindow.axaml -->
<Border Grid.Row="0">
    <!-- Existing UI -->
    <Grid>
        <!-- Left sidebar, message panel, right panel — existing -->

        <!-- NEW: Pingu avatar (bottom-right overlay) -->
        <Controls:PinguAvatar x:Name="PinguAvatar"
                              Grid.Row="0"
                              HorizontalAlignment="Right"
                              VerticalAlignment="Bottom"
                              Margin="0,0,16,16" />
    </Grid>
</Border>
```

### MainWindow.axaml.cs — Initialize Pingu on startup

```csharp
// src/Desktop/MainWindow.axaml.cs
public MainWindow(
    ILogger<MainWindow>? logger,
    IConversationManager? conversationManager = null,
    IServerService? serverService = null,
    IModelRepository? modelRepository = null,
    IChatCompletionService? chatCompletionService = null,
    IChatContextManager? contextManager = null,
    IContextWindowBudgeter? budgeter = null,
    IPinguStore? pinguStore = null)  // NEW
{
    InitializeComponent();

    // Existing initialization...

    // NEW: Wire up Pingu
    PinguAvatar = pinguStore != null ? new Controls.PinguAvatar(pinguStore) : null;
    if (PinguAvatar != null)
    {
        PinguAvatar.OnStateChanged += OnPinguStateChanged;
        AddChild(PinguAvatar);
    }

    // NEW: Auto-awaken Pingu if models exist
    if (pinguStore != null && modelRepository != null)
    {
        _ = Task.Run(async () => {
            var models = await modelRepository.DiscoverModelsAsync();
            if (models.Any(m => m.Type == ModelType.TextGeneration))
            {
                await pinguStore.SetAwakeAsync(true);
            }
        });
    }
}
```

---

## 11. Project File Updates

### src/Domain/OpenLMStudio.Domain.csproj

Add Pingu and QEMU models:

```xml
<ItemGroup>
  <Compile Include="Models\Pingu\PinguState.cs" />
  <Compile Include="Models\Pingu\PinguMood.cs" />
  <Compile Include="Models\Pingu\PinguPanelType.cs" />
  <Compile Include="Models\Pingu\AwakeningPhase.cs" />
  <Compile Include="Models\QEMU\QEMUTypes.cs" />
  <Compile Include="Models\QEMU\VMInstance.cs" />
  <Compile Include="Models\QEMU\VMCreationConfig.cs" />
</ItemGroup>
```

### src/Application/OpenLMStudio.Application.csproj

Add interfaces:

```xml
<ItemGroup>
  <Compile Include="Interfaces\IPinguStore.cs" />
  <Compile Include="Interfaces\ISystemAIClient.cs" />
  <Compile Include="Interfaces\IQEMUProcessManager.cs" />
  <Compile Include="Interfaces\IToolchainRegistry.cs" />
  <Compile Include="Interfaces\IArchPromptService.cs" />
</ItemGroup>
```

### src/Infrastructure/OpenLMStudio.Infrastructure.csproj

Add services:

```xml
<ItemGroup>
  <Compile Include="Services\PinguStore.cs" />
  <Compile Include="Services\SystemAIClient.cs" />
  <Compile Include="Services\QEMU\QEMUProcessManager.cs" />
  <Compile Include="Services\QEMU\QMPClient.cs" />
  <Compile Include="Services\QEMU\ArchPromptService.cs" />
  <Compile Include="Services\ToolchainRegistry.cs" />
  <Compile Include="Services\ContextCompressionService.cs" />
  <Compile Include="Services\PromptEngineService.cs" />
  <Compile Include="Services\SkillDiscoveryService.cs" />
</ItemGroup>
```

### src/Desktop/OpenLMStudio.Desktop.csproj

Add UI components:

```xml
<ItemGroup>
  <AvaloniaResource Include="Controls\PinguAvatar.axaml" />
  <Compile Include="Controls\PinguAvatar.axaml.cs" />
  <AvaloniaResource Include="Controls\PinguPanel.axaml" />
  <Compile Include="Controls\PinguPanel.axaml.cs" />
  <AvaloniaResource Include="Controls\PinguHomeTile.axaml" />
  <AvaloniaResource Include="Windows\VMCreationWizardWindow.axaml" />
  <Compile Include="Windows\VMCreationWizardWindow.axaml.cs" />
  <AvaloniaResource Include="Controls\VMConsole.axaml" />
  <Compile Include="Controls\VMConsole.axaml.cs" />
</ItemGroup>
```

---

## 12. QEMU IPC Bridge (Avalonia → QEMU)

Since OpenLMStudio runs natively (not Electron), QEMU processes are spawned directly:

```csharp
// src/Infrastructure/Services/QEMU/QEMUProcessManager.cs
// QEMU processes are spawned via Process.Start() — no IPC bridge needed

// For cross-process communication (e.g., from another app), expose via:
// - Named pipes (Windows) / Unix sockets (Linux/macOS)
// - gRPC server (optional, for remote VM management)
// - REST API endpoint (optional)
```

---

## 13. Pingu Mini-Games (Optional)

Pingu's idle behavior includes playing mini-games. In Avalonia:

```
src/Desktop/Controls/
├── MinesweeperGame.axaml
├── TetrisGame.axaml
└── SnakeGame.axaml
```

---

## 14. VMStore (Zustand equivalent for VM state)

Port `vmStore.ts` (Zustand store for managed VM instances):

```csharp
// src/Infrastructure/Services/VMStore.cs
namespace OpenLMStudio.Infrastructure.Services;

public class VMStore : IVMStore
{
    private readonly ConcurrentDictionary<string, VMInstance> _instances = new();

    public IEnumerable<VMInstance> Instances => _instances.Values;

    public async Task AddAsync(VMInstance vm)
    {
        _instances.AddOrUpdate(vm.Id, vm, (k, v) => vm);
        await NotifyChangedAsync();
    }

    public async Task RemoveAsync(string vmId)
    {
        _instances.TryRemove(vmId, out _);
        await NotifyChangedAsync();
    }

    public async Task<VMInstance?> GetAsync(string vmId)
    {
        _instances.TryGetValue(vmId, out var vm);
        return vm;
    }
}
```

---

## 15. VMPanel — Sidebar Tab Component

Planned `src/Desktop/Controls/VMPanel.axaml` — Connected to sidebar tab system alongside Tasks/MCP panels:

```xml
<!-- src/Desktop/Controls/VMPanel.axaml -->
<UserControl x:Class="OpenLMStudio.Desktop.Controls.VMPanel">
    <StackPanel>
        <!-- VM list (from VMStore) -->
        <ListBox x:Name="VmList" ItemsSource="{Binding VMs}" />
        <!-- VM controls: Start, Stop, Pause, Resume -->
        <StackPanel Orientation="Horizontal">
            <Button Content="Start" Click="OnVmStartClicked" />
            <Button Content="Stop" Click="OnVmStopClicked" />
            <Button Content="Pause" Click="OnVmPauseClicked" />
            <Button Content="Resume" Click="OnVmResumeClicked" />
        </StackPanel>
        <!-- VM console (serial output) -->
        <TextBox x:Name="VmConsole" TextWrapping="Wrap" IsReadOnly="True" AcceptsReturn="True" />
    </StackPanel>
</UserControl>
```

---

## 16. System AI Full Control Mode — Port of SystemAICoordinator

```
src/Application/Interfaces/
└── ISystemAICoordinator.cs

src/Infrastructure/Services/
└── SystemAICoordinator.cs
```

```csharp
// src/Infrastructure/Services/SystemAICoordinator.cs
namespace OpenLMStudio.Infrastructure.Services;

public class SystemAICoordinator : ISystemAICoordinator
{
    private readonly ISystemAIClient _systemAI;
    private readonly IQEMUProcessManager _qemuManager;
    private readonly IPromptEngine _promptEngine;

    public async Task<Workflow> HandleCommandAsync(string command)
    {
        var intent = await Nlp.ParseAsync(command);

        if (intent.Type == "bugFixing")
        {
            var vm = await GetOrCreateArchVMAsync(intent.Architecture ?? "x86_64");
            return await BugFixingWorkflow.ExecuteAsync(intent, new { VM = vm });
        }
        else if (intent.Type == "crossCompile")
        {
            return await CrossCompilationWorkflow.ExecuteAsync(intent, new
            {
                SourceArch = intent.FromArchitecture ?? "x86_64",
                TargetArch = intent.ToArchitecture ?? "aarch64"
            });
        }
    }

    public async Task<VMInstance> GetOrCreateArchVMAsync(ArchitectureType arch)
    {
        var existing = _qemuManager.Instances
            .Values
            .FirstOrDefault(vm => vm.Architecture == arch && vm.State == VMRunState.Running);

        return existing ?? await CreateArchVMAsync(arch);
    }
}
```

---

## 17. Pingu Automation — Port of PinguAutomation

```
src/Infrastructure/Services/
└── PinguAutomation.cs
```

```csharp
// src/Infrastructure/Services/PinguAutomation.cs
namespace OpenLMStudio.Infrastructure.Services;

public class PinguAutomation : IPinguAutomation
{
    private readonly IPinguStore _pingu;
    private readonly IQEMUProcessManager _qemuManager;
    private readonly IUIManager _uiManager;
    private readonly IAvatarRenderer _avatarRenderer;

    public async Task EnterControlModeAsync()
    {
        // Grey out UI elements — including QEMU VM panel controls
        await _uiManager.GreyOutUIAsync();

        // Start animated Pingu walking around — with VM management actions
        await _avatarRenderer.StartWalkingAnimationAsync(new
        {
            Architectures = _qemuManager.Instances.Values.Select(v => v.Architecture).ToList()
        });
    }

    public async Task HandleDragToPauseAsync(DragEvent e)
    {
        if (e.Target == "pingu")
        {
            // Pause all running QEMU VMs when user drags Pingu to corner
            foreach (var vm in _qemuManager.Instances.Values.Where(v => v.State == VMRunState.Running))
                await _qemuManager.PauseVMAsync(vm.Id);

            await ReturnPinguToCornerAsync();
        }
    }

    public async Task PerformActionAsync(string action, Element? target = null)
    {
        var animation = await _avatarRenderer.CreateActionAnimationAsync(action);

        if (target != null)
            await HighlightTargetAsync(target);

        // Execute QEMU command based on action type
        if (action == "startVM")
        {
            var vmId = target?.Dataset?.VmId;
            if (vmId != null)
                await _qemuManager.StartVMAsync(vmId);
        }
        else if (action == "stopVM")
        {
            var vmId = target?.Dataset?.VmId;
            if (vmId != null)
                await _qemuManager.StopVMAsync(vmId);
        }
    }
}
```

---

## 18. Resource Manager — Port of ResourceManager

```
src/Infrastructure/Services/
└── ResourceManager.cs
```

```csharp
// src/Infrastructure/Services/ResourceManager.cs
namespace OpenLMStudio.Infrastructure.Services;

public class ResourceManager : IResourceManager
{
    private readonly IQEMUProcessManager _qemuManager;
    private readonly IModelEngine _engine;

    public async Task<ResourceMetrics> MonitorResourcesAsync()
    {
        return new ResourceMetrics
        {
            Cpu = await GetCpuUsageAsync(),
            Memory = await GetMemoryUsageAsync(),
            Gpu = await GetGpuUsageAsync(),
            Disk = await GetDiskSpaceAsync(),
            VmInstances = _qemuManager.Instances.Values.Select(vm => new
            {
                vm.Id,
                vm.Architecture,
                vm.RamBytes,
                vm.State,
                CpuCores = vm.CpuTopology.Sockets ?? 1,
            }).ToList()
        };
    }

    public async Task AdjustModelSettingsAsync(ResourceMetrics metrics)
    {
        if (metrics.Cpu > 90)
        {
            // Reduce parallel inference, switch to smaller model — including reducing VM CPU cores
            await _engine.AdjustAsync(new ModelSettings
            {
                MaxThreads = 2,
                ModelSize = "small",
                GpuLayers = (int)(metrics.Gpu.Memory * 0.8),
                VmCpuCoreLimit = metrics.VmInstances
                    .Where(vm => vm.CpuCores > 4 && vm.State == VMRunState.Running)
                    .Sum(vm => vm.CpuCores - 2)
            });
        }
    }

    public async Task CompactPromptIfNeededAsync(int contextLength)
    {
        if (contextLength > MAX_CONTEXT)
            await _contextCompressionEngine.CompressAsync();  // Compress QEMU console output per VM architecture context
    }
}
```

---

## 19. Implementation Priority Matrix

| Phase | Features | Estimated Duration |
|-------|----------|-------------------|
| **1** | Pingu Store + Avatar UI | Weeks 1-2 |
| **2** | System AI Client | Weeks 3-4 |
| **F.1** | Core QEMU Integration (VM types, process management, QMP protocol) | Weeks 5-6 |
| **F.2** | Architecture Support Matrix (x86_64/KVM, ARM64/RISC-V/AVR/MIPS/PPC/SPARC) | Weeks 7-8 |
| **F.3** | Native Tooling Management per architecture (GCC cross-compilers, AVR toolchain) | Weeks 9-10 |
| **G** | Advanced Search & Navigation — with VM architecture context filtering | Weeks 11-16 |
| **H** | Prompt Engineering Assistant — with QEMU architecture-aware prompts | Weeks 17-22 |
| **I** | System AI Resource Management — including QEMU VM resource tracking | Weeks 23-28 |
| **J** | CI/CD Integration Suite — with QEMU multi-architecture testing matrix | Weeks 29-34 |
| **K** | Analytics & Insights Dashboard — with QEMU VM metrics | Weeks 35-40 |
| **L** | API Documentation Tools — including QEMU QMP command reference | Weeks 41-46 |
| **M** | System AI Full Control Mode — with QEMU architecture-aware commands | Weeks 47-52 |
| **N** | Pingu Automation & UI — with VM management animations per architecture | Weeks 53-56 |

---

## 20. Dependencies (NuGet)

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.Logging.Abstractions` | ILogger (already used) |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | DI interfaces (already used) |
| `CommunityToolkit.Mvvm` | Optional: reactive properties (replaces Zustand) |
| `SkiaSharp` | Pingu SVG rendering (optional) |
| `Tmds.DBus` | Linux-specific process management (optional) |
| `System.IO.Pipes` | Named pipes for IPC (optional) |
| `FluentValidation` | Schema validation (replaces zod) |

---

## 21. Key Mappings (TypeScript → C#)

| TypeScript | C# (.NET 8) |
|---|---|
| `spawn()` | `Process.Start()` |
| `net.connect()` | `TcpClient.ConnectAsync()` |
| `zod` schemas | C# records + `FluentValidation` |
| `Zustand` store | `IPinguStore` + events |
| `setInterval` | `System.Threading.Timer` |
| `Buffer` | `Memory<byte>` |
| `child_process` | `System.Diagnostics.Process` |
| `chokidar` | `System.IO.FileSystemWatcher` |
| `axios` streaming | `HttpClient` + `Stream` |
| `node-pty` | `Process` + `PipeStream` |
| `signalR` | Not needed (local process) |
| `electron/preload.ts` | Not needed (native app) |
| `electron/main.ts` | `App.axaml.cs` (main entry) |
| `.axaml` (XAML) | `.tsx` (React) |
| CSS animations | Avalonia `Storyboard` |

---

## 22. Key Design Decisions from QEMU API Research

1. **AVR special case**: No `-machine` flag, no disk images — runs directly from flash via `-bios firmware.hex` (bare-metal MCU)
2. **EDK2 UEFI for ARM/RISC-V**: Required per `-bios` docs in System chapter; auto-detected from `EDK2_DIR` environment variable
3. **qcow2 vs raw disk formats**: qcow2 for snapshot support on slow TCG VMs; raw for embedded boards where qcow2 isn't supported (e.g., RISC-V microvm eMMC)
4. **Per-architecture NIC models**: Derived from default machine type docs in QEMU System chapter — varies significantly between architectures
5. **KVM availability check**: Per `/dev/kvm` device node and `kvm-ok` command in QEMU KVM documentation
6. **QMP protocol handshake required**: Capability negotiation must complete before any commands (per QMP spec Protocol Specification section)
7. **TCG multi-threading support**: `-thread=single|multi` option from TCG docs for parallel execution on host cores
8. **Per-architecture CPU hotplug**: Via `device_add driver=cpu` QMP command — supported by machine types that have `cpu-hotplug` property

---

## 23. Notes

1. **No Electron needed** — OpenLMStudio runs natively via Avalonia. QEMU processes are spawned directly from the main process.
2. **No IPC bridge** — Unlike the Electron architecture (which needed preload.ts → main.ts IPC), C# services communicate directly through DI.
3. **Thread safety** — Use `ConcurrentDictionary` for shared state, `Dispatcher.UIThread.InvokeAsync()` for UI updates.
4. **Cross-platform** — QEMU binaries and toolchains are platform-specific; detect at runtime and download accordingly.
5. **Error handling** — Follow the existing pattern: try/catch with `_logger?.LogError()` and graceful degradation.
6. **Testing** — Add unit tests in `tests/OpenLMStudio.Infrastructure.Tests/` for QEMUProcessManager and SystemAIClient.