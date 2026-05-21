# Plugin Development Guide

## Overview

OpenLMStudio uses a plugin system built on the `IPluginRegistry` interface and `IPlugin` contract. Plugins are discovered from the plugin registry directory and can extend the core functionality with custom tools, services, or integrations.

## Plugin Architecture

### Plugin Registry

Located at `src/Infrastructure/Services/PluginRegistry.cs`, the registry:

- Discovers plugins from the `plugins/` subdirectory in the user's AppData folder
- Loads plugin manifests (JSON files) that describe plugin metadata and entry points
- Manages installation, updates, and enable/disable state
- Enforces sandbox policies on install (restricts access to filesystem, network, etc.)
- Validates plugin hashes on update for security

### Plugin Manifest

Each plugin has a `plugin.json` manifest:

```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "description": "A sample plugin",
  "entryPoint": "MyPlugin.dll",
  "enabled": true,
  "sandboxPolicy": "default",
  "dependencies": []
}
```

### Plugin Interface

Plugins implement `IPlugin` from `src/Application/Interfaces/IPluginRegistry.cs`:

```csharp
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    Task InitializeAsync(IServiceProvider services);
    void Dispose();
}
```

## Creating a Plugin

### Step 1: Create the Plugin Project

```bash
dotnet new classlib -n MyPlugin -o ../plugins/MyPlugin
cd ../plugins/MyPlugin
dotnet add package OpenLMStudio.Application
dotnet add package OpenLMStudio.Domain
```

### Step 2: Implement the Plugin

```csharp
using OpenLMStudio.Application.Interfaces;

namespace MyPlugin;

public class MyPlugin : IPlugin
{
    public string Name => "MyPlugin";
    public string Version => "1.0.0";
    public string Description => "A custom plugin example";

    public async Task InitializeAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<MyPlugin>>();
        logger.LogInformation("MyPlugin initialized");
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
```

### Step 3: Add the Manifest

Create `plugin.json` in the plugin directory:

```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "description": "A custom plugin example",
  "entryPoint": "MyPlugin.dll",
  "enabled": true,
  "sandboxPolicy": "default",
  "dependencies": []
}
```

### Step 4: Build and Install

```bash
dotnet build -c Release
# Copy to plugin directory
cp -r bin/Release/net8.0/* ~/.local/share/OpenLMStudio/plugins/MyPlugin/
```

## Plugin Types

### Tool Plugins

Tool plugins extend the agent harness by implementing `ITool`:

```csharp
public class MyTool : ITool
{
    public string Name => "my_tool";

    public string GetParameterSchema() => """
        {
          "prompt": {"type": "string", "description": "The text to analyze"}
        }
        """;

    public async Task<ToolResult> ExecuteAsync(string parameters)
    {
        var input = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parameters)!;
        var prompt = (string)input["prompt"];

        return new ToolResult { Output = $"Analysis: {prompt.Length} characters" };
    }
}
```

### Service Plugins

Service plugins register custom services via the application's DI container:

```csharp
public class MyServicePlugin : IPlugin
{
    public async Task InitializeAsync(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IPluginRegistry>();
        registry.RegisterService(typeof(IMyService), typeof(MyService));
    }
}
```

## Security and Sandboxing

Plugins can be configured with a sandbox policy:

- **`restricted`**: No filesystem or network access
- **`default`**: Limited filesystem access within the plugin directory
- **`full`**: Full access to system resources

The sandbox policy is enforced by `SandboxService` which uses:
- cgroups v2 on Linux/macOS for process isolation
- Job Objects on Windows for process grouping

## Testing Plugins

1. Build your plugin in Release mode
2. Copy to the plugin directory
3. Enable in the Plugin Management Panel (Settings → Plugins)
4. Verify in the UI that the plugin is loaded

## Remote Plugin Registry

The `PluginRegistry` supports remote registry URLs for discovering plugins from GitHub or custom registries:

```csharp
registry.SetRegistryUrl("https://example.com/plugins/registry.json");
var available = await registry.GetAvailableUpdatesAsync();
```

## Plugin Lifecycle Events

| Event | Description |
|-------|-------------|
| `Install` | Plugin is installed from manifest |
| `Enable` | Plugin is enabled via UI or API |
| `Disable` | Plugin is disabled |
| `Update` | Plugin is updated to a newer version |
| `Uninstall` | Plugin is removed |

## Examples

### Example: Git Tool Plugin

```csharp
public class GitToolPlugin : IPlugin
{
    public string Name => "GitToolPlugin";

    public async Task InitializeAsync(IServiceProvider services)
    {
        var toolRegistry = services.GetRequiredService<IToolRegistry>();
        toolRegistry.RegisterTool(new GitDiffTool());
        toolRegistry.RegisterTool(new GitLogTool());
    }
}
```

### Example: Model Monitor Plugin

```csharp
public class ModelMonitorPlugin : IPlugin
{
    public async Task InitializeAsync(IServiceProvider services)
    {
        var deviceMonitor = services.GetRequiredService<IDeviceMonitor>();
        deviceMonitor.DeviceChanged += OnDeviceChanged;
    }

    private void OnDeviceChanged(object? sender, DeviceChangedEventArgs e)
    {
        // Log VRAM usage, etc.
    }
}