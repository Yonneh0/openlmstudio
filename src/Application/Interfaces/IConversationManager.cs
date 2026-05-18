using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application.Interfaces;

// ---- Server-related types (used by IServerService) - kept here for backward compatibility ----

/// <summary>
/// Configuration for the local inference server.
/// </summary>
public class ServerConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool UseHttps { get; set; } = true;
    public int MaxConcurrentRequests { get; set; } = 8;
    public int TimeoutSeconds { get; set; } = 60;
    public bool AllowCors { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Represents the current state of the server service.
/// </summary>
public enum ServerState
{
    /// <summary>
    /// The server is not running.
    /// </summary>
    Stopped,

    /// <summary>
    /// The server is starting up.
    /// </summary>
    Starting,

    /// <summary>
    /// The server is actively listening for connections.
    /// </summary>
    Running,

    /// <summary>
    /// The server is shutting down gracefully.
    /// </summary>
    Stopping,

    /// <summary>
    /// An error occurred during operation.
    /// </summary>
    Error
}

/// <summary>
/// Event arguments for server state changes.
/// </summary>
public class ServerStateChangedEventArgs : EventArgs
{
    public ServerState OldState { get; set; }
    public ServerState NewState { get; set; }
    public string? Message { get; set; }
}

// ---- End of Server-related types ----

// Note: DeviceInfo, GpuDevice, and CpuInfo are now exclusively defined in Domain.Models namespace.
// The Application layer no longer defines these types - use OpenLMStudio.Domain.Models.DeviceInfo, etc.

/// <summary>
/// Manages chat creation, loading, and persistence operations.
/// </summary>
public interface IConversationManager : IDisposable
{
    /// <summary>
    /// Creates a new chat session with the specified parameters.
    /// </summary>
    /// <param name="name">Display name for the chat.</param>
    /// <param name="modelId">Optional model ID to associate (null uses system default).</param>
    /// <returns>The created chat entity.</returns>
    Task<Chat> CreateChatAsync(string name, string? modelId = null);

    /// <summary>
    /// Loads an existing chat by its identifier.
    /// </summary>
    /// <param name="chatId">The unique identifier of the chat to load.</param>
    /// <returns>Awaitable task returning the loaded chat, or null if not found.</returns>
    Task<Chat?> LoadChatAsync(Guid chatId);

    /// <summary>
    /// Lists all available chats on the system.
    /// </summary>
    /// <returns>List of all chat entities sorted by last activity (newest first).</returns>
    Task<IEnumerable<Chat>> ListChatsAsync();

    /// <summary>
    /// Deletes a chat and its associated message files from storage.
    /// </summary>
    /// <param name="chatId">The unique identifier of the chat to delete.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task DeleteChatAsync(Guid chatId);

    /// <summary>
    /// Adds a message to an existing chat session.
    /// </summary>
    /// <param name="chatId">The chat to add the message to.</param>
    /// <param name="message">The message to save.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task AddMessageAsync(Guid chatId, Message message);

    /// <summary>
    /// Retrieves all messages for a given chat in chronological order.
    /// </summary>
    /// <param name="chatId">The chat to retrieve messages from.</param>
    /// <param name="limit">Optional maximum number of recent messages to return.</param>
    /// <returns>List of message entities ordered chronologically (oldest first).</returns>
    Task<List<Message>> GetMessagesAsync(Guid chatId, int? limit = null);

    /// <summary>
    /// Searches for chats matching the given query term.
    /// </summary>
    /// <param name="query">Search text to match against chat names and tags.</param>
    /// <returns>List of matching chats.</returns>
    Task<IEnumerable<Chat>> SearchChatsAsync(string query);

    /// <summary>
    /// Updates the metadata for an existing chat.
    /// </summary>
    /// <param name="chatId">The chat to update.</param>
    /// <param name="updates">Anonymous object with properties to change.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task UpdateChatAsync(Guid chatId, object updates);

    /// <summary>
    /// Calculates the total token count for a chat session.
    /// </summary>
    /// <param name="chatId">The chat to calculate tokens for.</param>
    /// <returns>Total approximate token count across all messages.</returns>
    Task<int> CalculateTotalTokenCountAsync(Guid chatId);

    /// <summary>
    /// Exports a chat's messages to a JSON file.
    /// </summary>
    /// <param name="chatId">The chat to export.</param>
    /// <param name="destinationPath">File path for the exported JSON.</param>
    /// <returns>Awaitable task indicating completion.</returns>
    Task ExportChatAsync(Guid chatId, string destinationPath);

    /// <summary>
    /// Imports a chat from a previously exported JSON file.
    /// </summary>
    /// <param name="sourcePath">File path of the exported JSON to import.</param>
    /// <returns>Awaitable task returning the imported chat entity.</returns>
    Task<Chat?> ImportChatAsync(string sourcePath);

    /// <summary>
    /// Gets token count for a specific conversation by string ID.
    /// </summary>
    long GetConversationTokenCount(string chatId);
}

/// <summary>
/// Interface for monitoring hardware devices (GPU, CPU, memory).
/// Uses Domain model types exclusively - no duplicate definitions.
/// </summary>
public interface IDeviceMonitor : IDisposable
{
    /// <summary>
    /// Gets the current device information snapshot from the domain layer.
    /// </summary>
    DeviceInfo CurrentDeviceInformation { get; }

    /// <summary>
    /// Event raised when device metrics change.
    /// </summary>
    event Action<DeviceInfo>? DeviceInfoChanged;

    /// <summary>
    /// Starts monitoring device metrics at specified interval.
    /// </summary>
    void StartMonitoring(TimeSpan? updateInterval = null);

    /// <summary>
    /// Stops monitoring device metrics.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Gets information about available GPU devices.
    /// </summary>
    Task<IEnumerable<GpuDevice>> GetGpuDevicesAsync();

    /// <summary>
    /// Detects all available GPUs on the system.
    /// </summary>
    Task<IEnumerable<GpuDevice>> DetectAllGpusAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets current CPU utilization percentage.
    /// </summary>
    double GetCpuUtilization();

    /// <summary>
    /// Gets available system memory in bytes.
    /// </summary>
    long GetAvailableMemoryBytes();

    /// <summary>
    /// Checks if a specific GPU model supports CUDA acceleration.
    /// </summary>
    Task<bool> IsGpuCudaCompatibleAsync(string gpuName);

    /// <summary>
    /// Gets recommended memory split ratio for CPU/GPU offloading.
    /// </summary>
    double GetRecommendedGpuMemoryRatio();
}

/// <summary>
/// Manages the lifecycle of the local inference server.
/// Uses ServerConfiguration from this namespace - no duplicate definitions.
/// </summary>
public interface IServerService : IDisposable
{
    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current state of the server service.
    /// </summary>
    ServerState State { get; }

    /// <summary>
    /// Gets the base URL where the server is listening (null if not running).
    /// </summary>
    string? BaseUrl => State == ServerState.Running
        ? $"{(Configuration.UseHttps ? "https" : "http")}://{Configuration.Host}:{Configuration.Port}"
        : null;

    /// <summary>
    /// Gets the server configuration.
    /// </summary>
    ServerConfiguration Configuration { get; }

    /// <summary>
    /// Starts the local server with the given configuration.
    /// </summary>
    Task StartAsync(ServerConfiguration? config = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the server gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a port is currently in use.
    /// </summary>
    Task<bool> IsPortInUseAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an available port starting from the given number.
    /// Increments until finding a free port or reaching the limit.
    /// </summary>
    Task<int> FindAvailablePortAsync(int startPort = 0, int maxAttempts = 100);

    /// <summary>
    /// Generates a self-signed certificate for HTTPS development.
    /// </summary>
    Task GenerateSelfSignedCertificateAsync(string certificatePath, string keyPath, CancellationToken ct = default);

    /// <summary>
    /// Event raised when the server state changes from one state to another.
    /// </summary>
    event EventHandler<ServerStateChangedEventArgs>? StateChanged;
}

/// <summary>
/// Interface for MCP (Model Context Protocol) client communication.
/// </summary>
public interface IMcpClient : IDisposable
{
    /// <summary>
    /// Gets whether this MCP client is currently connected to a server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to an MCP server via the specified transport mode.
    /// </summary>
    Task ConnectAsync(string command, string[] args, string transportMode = "stdio");

    /// <summary>
    /// Disconnects from the MCP server.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Lists all available tools exposed by the connected MCP server.
    /// </summary>
    Task<IEnumerable<McpToolDefinition>> ListToolsAsync();

    /// <summary>
    /// Calls a tool on the connected MCP server.
    /// </summary>
    Task<string?> CallToolAsync(string toolName, string argumentsJson);
}

/// <summary>
/// Definition of an MCP tool exposed by a connected server.
/// </summary>
public record McpToolDefinition(
    string Name,
    string Description,
    Dictionary<string, object>? InputSchema
);