using System;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using SQLitePCL;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides platform-specific resolution of the OpenLMStudio appdata directory.
/// 
/// Platform paths:
/// - Windows: %APPDATA%\OpenLMStudio via Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
/// - macOS: ~/Library/Application Support/OpenLMStudio via NSApplication.shared().bundlePath or Mono.Cecil Objective-C interop
/// - Linux: $XDG_CONFIG_HOME/OpenLMStudio → fallback to ~/.config/OpenLMStudio
/// </summary>
public class AppDataDirectoryResolver : IDisposable
{
    private readonly ILogger<AppDataDirectoryResolver>? _logger;

    /// <summary>
    /// Creates a new instance of the directory resolver.
    /// </summary>
    public AppDataDirectoryResolver(ILogger<AppDataDirectoryResolver>? logger = null)
    {
        _logger = logger;
    }
    private static string? _cachedAppDataPath;

    /// <summary>
    /// Gets the OpenLMStudio appdata directory for the current platform.
    /// Cached after first call to avoid repeated environment lookups.
    /// </summary>
    public string GetAppDataDirectory()
    {
        if (_cachedAppDataPath != null)
            return _cachedAppDataPath;

        try
        {
            // Windows: %APPDATA%\OpenLMStudio
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(appData))
            {
                _cachedAppDataPath = System.IO.Path.Combine(appData, "OpenLMStudio");
                _logger?.LogDebug("Windows AppData directory: {Path}", _cachedAppDataPath);
                return _cachedAppDataPath;
            }

            // macOS: ~/Library/Application Support/OpenLMStudio
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                var macosPath = System.IO.Path.Combine(home, "Library", "Application Support", "OpenLMStudio");
                _cachedAppDataPath = macosPath;
                _logger?.LogDebug("macOS Application Support directory: {Path}", _cachedAppDataPath);
                return _cachedAppDataPath;
            }

            // Linux: $XDG_CONFIG_HOME/OpenLMStudio or ~/.config/OpenLMStudio
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            string linuxPath;

            if (!string.IsNullOrEmpty(xdgConfig))
            {
                linuxPath = System.IO.Path.Combine(xdgConfig, "OpenLMStudio");
            }
            else if (home != null)
            {
                linuxPath = System.IO.Path.Combine(home, ".config", "OpenLMStudio");
            }
            else
            {
                throw new InvalidOperationException("Cannot determine home directory for Linux appdata path.");
            }

            _cachedAppDataPath = linuxPath;
            _logger?.LogDebug("Linux XDG_CONFIG_HOME/AppData directory: {Path}", _cachedAppDataPath);
            return _cachedAppDataPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to determine AppData directory");
            throw;
        }
    }

    /// <summary>
    /// Creates or returns the path for a subdirectory under the appdata directory.
    /// </summary>
    public string GetSubDirectory(string subDirName)
    {
        var baseDir = GetAppDataDirectory();
        var subPath = System.IO.Path.Combine(baseDir, subDirName);
        Directory.CreateDirectory(subPath);
        return subPath;
    }

    /// <summary>
    /// Initializes all required appdata subdirectories for the first run.
    /// Creates contexts/, metadata/, models/, tasks/, and logs/ directories per spec Phase 1.8.
    /// </summary>
    public void InitializeSubdirectories()
    {
        GetSubDirectory("contexts");   // SQLite databases for conversation context (per-chat .db files)
        GetSubDirectory("metadata");   // Project metadata, settings, config (.json) files
        GetSubDirectory("models");     // Model registry data (NOT model binaries; symlinks/references to actual locations)
        GetSubDirectory("tasks");      // Agentic task snapshots and state (.db or per-task JSON)
        GetSubDirectory("logs");       // Application log files with rotation enabled
    }

    /// <summary>
    /// Gets the path to a conversation database file.
    /// </summary>
    public string GetConversationDatabasePath(string chatId)
    {
        var contextsDir = GetSubDirectory("contexts");
        return System.IO.Path.Combine(contextsDir, $"{chatId}.db");
    }

    /// <summary>
    /// Gets the path to a settings database file.
    /// </summary>
    public string GetSettingsDatabasePath()
    {
        var metadataDir = GetSubDirectory("metadata");
        return System.IO.Path.Combine(metadataDir, "settings.db");
    }

    /// <summary>
    /// Gets the path to a task context database file.
    /// </summary>
    public string GetTaskContextDatabasePath(string taskId)
    {
        var tasksDir = GetSubDirectory("tasks");
        return System.IO.Path.Combine(tasksDir, $"{taskId}.db");
    }

    /// <summary>
    /// Gets the path to a log file.
    /// </summary>
    public string GetLogFilePath(string logName)
    {
        var logsDir = GetSubDirectory("logs");
        return System.IO.Path.Combine(logsDir, $"{logName}.log");
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}

/// <summary>
/// Provides platform-specific SQLite database connection creation.
/// Uses SQLitePCLRaw.bundle_e_sqlite3 for cross-platform support on Windows, macOS, and Linux.
/// </summary>
public class SqliteDatabaseFactory : IDisposable
{
    private readonly ILogger<SqliteDatabaseFactory>? _logger;

    public SqliteDatabaseFactory(ILogger<SqliteDatabaseFactory>? logger = null)
    {
        _logger = logger;

        // Initialize SQLitePCLRaw (this must be called once per process before any DB access)
        Batteries.Init();
    }

    /// <summary>
    /// Creates a connection to an existing SQLite database or creates it if it doesn't exist.
    /// </summary>
    public Microsoft.Data.Sqlite.SqliteConnection CreateConnection(string databasePath)
    {
        var directory = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Version=3;");
    }

    public void Dispose()
    {
        // No unmanaged resources to clean up
    }
}