// Avalonia Window code-behind — Helpers, server, devices, analysis, shortcuts, window state, games partial class
// Brought to you by Carls' Jr.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
    // ---- Message Sending ----

    private async void OnSendMessageClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedChatId == null || _conversationManager == null) return;

        var messageText = MessageInputBox?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(messageText))
            return;

        try
        {
            // Create user message
            var userMessage = new Message
            {
                Role = MessageRole.User,
                Content = messageText.Trim(),
                TokenCount = EstimateTokenCount(messageText),
                CreatedAt = DateTime.UtcNow
            };

            // Add the user's message to the conversation
            await _conversationManager.AddMessageAsync(_selectedChatId.Value, userMessage);

            // Clear input box
            MessageInputBox!.Text = string.Empty;

            // Display the user message
            var userBorder = CreateMessageBorder(userMessage);
            if (userBorder != null)
                MessageDisplayPanel?.Children.Add(userBorder);

            // Send to chat service and get response (fire-and-forget since it's async void)
            _ = Task.Run(async () =>
            {
                if (_conversationManager != null && _selectedChatId.HasValue)
                    await GetAssistantResponseAsync(_selectedChatId.Value, userMessage.Content!);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending message");
            ShowError($"Failed to send message: {ex.Message}");
        }
    }

    private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
    {
        // Send on Enter (without Shift for multi-line), or Ctrl+Enter always
        if (e.Key == Key.Enter && (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            OnSendMessageClicked(sender, e);
        }
    }

    // ---- Server Start/Stop Controls ----

    private async void OnServerStartStopClicked(object? sender, RoutedEventArgs e)
    {
        if (_serverService == null) return;

        var isRunning = _serverService.State != ServerState.Stopped;

        try
        {
            if (isRunning)
            {
                // Stop the server
                await _serverService.StopAsync();

                // Update UI to reflect stopped state
                if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = "Start Server";
                ServerStatusText.Text = "Server: Stopped";
                ServerStatusTextStatusBar.Text = "Server: Stopped";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            }
            else
            {
                // Start the server using the service's current config, or create one with default port
                var configuration = _serverService.Configuration;
                if (configuration == null)
                {
                    configuration = new ServerConfiguration { Port = 8080 };
                }
                await _serverService.StartAsync(configuration);

                // Update UI to reflect running state
                if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = "Stop Server";
                ServerStatusText.Text = $"Server: Running (Port {configuration.Port})";
                ServerStatusTextStatusBar.Text = $"Server: Running (Port {configuration.Port})";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }

            UpdateServerStatus();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error toggling server state");
            ShowError($"Server error: {ex.Message}");
        }
    }

    private void UpdateServerStatus()
    {
        if (_serverService == null) return;

        var isRunning = _serverService.State != ServerState.Stopped;

        // Update server status display
        ServerStatusText.Text = $"Server: {(isRunning ? "Running" : "Stopped")}";
        ServerStatusTextStatusBar.Text = $"Server: {(isRunning ? "Running" : "Stopped")}";

        if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = isRunning ? "Stop Server" : "Start Server";

        if (isRunning)
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            ServerStatusTextStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green

            // Try to get port from the server service's configuration (use interface property, not unsafe cast)
            if (_serverService.Configuration != null)
            {
                ServerPortText.Text = $"Port: {_serverService.Configuration.Port}";
            }
        }
        else
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
            ServerStatusTextStatusBar.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
            ServerPortText.Text = "Port: 8080 (default)";
        }
    }

    private void OnServerStateChanged(object? sender, ServerStateChangedEventArgs e)
    {
        // Update UI on server state changes from the service itself — use Avalonia's UIThread dispatcher
        Dispatcher.UIThread.Invoke(() => UpdateServerStatus());

        if (e.NewState == ServerState.Error && !string.IsNullOrEmpty(e.Message))
        {
            Dispatcher.UIThread.Invoke(() => ShowError($"Server error: {e.Message}"));
        }
    }

    // ---- Model List Display ----

    private async void RefreshModelListAsync()
    {
        if (_modelRepository == null) return;

        try
        {
            var models = await _modelRepository.DiscoverModelsAsync();

            // Clear existing content from the scrollviewer and add model list
            if (ModelsTabContent?.Children.Count > 0)
            {
                ModelsTabContent.Children.Clear();
            }

            if (!models.Any())
            {
                ModelsTabContent?.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 8, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "No models discovered. Add GGUF or safetensors files to the model directory.",
                        Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                        FontSize = 14
                    }
                });
                return;
            }

            foreach (var model in models.OrderByDescending(m => m.Name))
            {
                var modelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var modelStack = new StackPanel();

                // Model name
                modelStack.Children.Add(new TextBlock
                {
                    Text = model.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14
                });

                // Path
                if (!string.IsNullOrEmpty(model.FilePath))
                {
                    modelStack.Children.Add(new TextBlock
                    {
                        Text = model.FilePath,
                        Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 6)
                    });
                }

                // Metadata summary
                var sizeMB = model.FileSizeBytes / 1024 / 1024;
                var metaText = $"Size: {sizeMB} MB | Type: {model.Type}";
                modelStack.Children.Add(new TextBlock
                {
                    Text = metaText,
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                modelBorder.Child = modelStack;
                ModelsTabContent?.Children.Add(modelBorder);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing model list");
        }
    }

    // ---- Utility Methods ----

    private int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + 3) / 4);

    /// <summary>
    /// Scrolls the message display ScrollViewer to the bottom.
    /// </summary>
    private async Task ScrollToBottomAsync()
    {
        var scrollViewer = FindScrollViewer(MessageDisplayPanel);
        if (scrollViewer != null)
            await Dispatcher.UIThread.InvokeAsync(() => scrollViewer.ScrollToEnd());
    }

    /// <summary>
    /// Finds a ScrollViewer by recursively searching Panel descendants.
    /// </summary>
    private static ScrollViewer? FindScrollViewer(Panel parent, int maxDepth = 10)
    {
        if (parent == null || maxDepth <= 0) return null;

        foreach (var child in parent.Children.OfType<Control>())
        {
            // Check direct descendants first
            if (child is ScrollViewer sv)
                return sv;

            // Then recurse into Panel children
            if (child is Panel panel)
            {
                var result = FindScrollViewer(panel, maxDepth - 1);
                if (result != null)
                    return result;
            }
        }

        return null;
    }

    private static T? FindDirectDescendant<T>(Panel parent) where T : Control
    {
        foreach (var child in parent.Children.OfType<Control>())
        {
            if (child is T typedChild) return typedChild;
        }
        return default;
    }

    // ---- Helper Methods ----

    /// <summary>
    /// Attempts to get the application's DI service provider from App.ApplicationServices.
    /// </summary>
    private static IServiceProvider? GetAppServiceProvider()
    {
        var appType = typeof(App);
        if (appType == null) return null;

        try
        {
            // Try to access ApplicationServices property via reflection
            var propInfo = appType.GetProperty("ApplicationServices",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return propInfo?.GetValue(null) as IServiceProvider;
        }
        catch
        {
            return null;
        }
    }

    private static T? FindChild<T>(Panel parent, int maxDepth = 10) where T : Control
    {
        if (parent == null || maxDepth <= 0) return default;

        foreach (var child in parent.Children.OfType<Control>())
        {
            if (child is T typedChild)
                return typedChild;

            // Only recurse into Panels since Control doesn't have Children in Avalonia
            if (child is Panel panel)
            {
                var result = FindChild<T>(panel, maxDepth - 1);
                if (result != null)
                    return result;
            }
        }

        return default;
    }

    private static Grid? FindGridInVisualTree(Visual parent, int maxDepth = 10)
    {
        if (parent == null || maxDepth <= 0) return null;

        foreach (var child in parent.GetVisualDescendants())
        {
            if (child is Grid grid)
                return grid;
        }

        return null;
    }

    /// <summary>
    /// Shows an error dialog using Avalonia's Window.ShowDialog().
    /// </summary>
    private void ShowError(string message)
    {
        try
        {
            // Try to find a parent window
            Window? parentWindow = null;

            if (this.Owner is Window w)
            {
                parentWindow = w;
            }
            else
            {
                // Try to find the first window via application lifetime
                var appLifetime = global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                parentWindow = appLifetime?.Windows.OfType<Window>().FirstOrDefault();
            }

            var errorWin = new Window
            {
                Title = "OpenLMStudio - Error",
                Width = 400,
                Height = 250,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 41)),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                        Padding = new Thickness(20),
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            if (parentWindow != null)
                errorWin.ShowDialog(parentWindow);
            else
                errorWin.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowError failed: {ex.Message}");
        }
    }

    // ---- DI Resolution Fallbacks ----

    /// <summary>
    /// Attempts to resolve the conversation manager from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IConversationManager? ResolveConversationManagerFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IConversationManager>();
    }

    /// <summary>
    /// Attempts to resolve the server service from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IServerService? ResolveServerServiceFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IServerService>();
    }

    /// <summary>
    /// Attempts to resolve the model repository from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IModelRepository? ResolveModelRepositoryFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IModelRepository>();
    }

    /// <summary>
    /// Attempts to resolve the chat completion service from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IChatCompletionService? ResolveChatCompletionServiceFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Attempts to resolve the chat context manager from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IChatContextManager? ResolveContextManagerFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IChatContextManager>();
    }

    /// <summary>
    /// Attempts to resolve the context window budgeter from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IContextWindowBudgeter? ResolveBudgeterFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IContextWindowBudgeter>();
    }

    /// <summary>
    /// Attempts to resolve the window settings service from App.ApplicationServices (DI fallback).
    /// </summary>
    private static IWindowSettings? ResolveWindowSettingsFromAppServices()
    {
        var sp = GetAppServiceProvider();
        return sp?.GetRequiredService<IWindowSettings>();
    }

    // ---- Device Status Update ----

    /// <summary>
    /// Handler for the refresh devices button — re-reads device state from hardware.
    /// </summary>
    private async void OnRefreshDevicesClicked(object? sender, RoutedEventArgs e)
    {
        _ = UpdateDeviceStatusAsync();
    }

    /// <summary>
    /// Reads device info from IDeviceMonitor and updates the UI elements.
    /// </summary>
    private async Task UpdateDeviceStatusAsync()
    {
        try
        {
            var serviceProvider = GetAppServiceProvider();
            if (serviceProvider == null) return;

            var deviceMonitor = serviceProvider.GetService<IDeviceMonitor>();
            if (deviceMonitor == null) return;

            var devices = deviceMonitor.CurrentDeviceInformation;

            // Update CPU info
            CpuCoreText.Text = $"CPU Cores: {devices.Cpu.LogicalProcessorCount} ({devices.Cpu.PhysicalCoreCount} physical)";

            // Update RAM info
            long ramBytes = Environment.WorkingSet;
            var ramGb = ramBytes > 0 ? (int)(ramBytes / (1024 * 1024 * 1024)) : 0;
            RamInfoText.Text = $"RAM: {ramGb} GB";

            // Update GPU info
            if (devices.Gpus.Any(g => g.TotalMemoryBytes > 0))
            {
                var gpu = devices.Gpus.First(g => g.TotalMemoryBytes > 0);
                LoadedModelRightText.Text = "GPU Detected";

                var gpuVramGb = gpu.TotalMemoryBytes / (1024 * 1024 * 1024);
                var gpuInfoText = $"{gpu.Name} ({gpuVramGb} GB VRAM)";
            }
            else
            {
                LoadedModelRightText.Text = "CPU Only";
            }

            // Update context length and offload info
            ContextLengthRightText.Text = "Context: 4096 tokens (default)";
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Error updating device status: {Message}", ex.Message);
            // Set fallback text
            CpuCoreText.Text = "CPU Cores: Unknown";
            RamInfoText.Text = "RAM: Unknown";
        }
    }

    // ---- Right Panel Tab Handlers (no-op — right panel is now a placeholder) ----

    private void OnRightContextTabClick(object? sender, RoutedEventArgs e) { }

    private void OnRightAnalysisTabClick(object? sender, RoutedEventArgs e) { }

    // ---- Keyboard Shortcuts (kept for early-init before KeyboardService is ready) ----
    // NOTE: KeyboardService now handles all keyboard shortcuts. This method is kept
    // as a no-op for backward compatibility with any code that might call it directly.
    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // No-op: KeyboardService now handles all keyboard shortcuts.
    }

    // ---- Window State Persistence ----

    /// <summary>
    /// Loads the previously saved window state and applies it to this window.
    /// Avalonia's Window doesn't expose Left/Top, so we only restore size and active tab.
    /// </summary>
    private async Task LoadWindowStateAsync()
    {
        if (_windowSettings == null) return;

        try
        {
            var state = await _windowSettings.LoadAsync();

            // Apply window dimensions
            Width = state.Width;
            Height = state.Height;

            // Restore active tab
            if (!string.IsNullOrEmpty(state.ActiveTab))
            {
                ShowTab(state.ActiveTab);
            }

            // Restore selected chat
            if (state.SelectedChatId.HasValue)
            {
                _selectedChatId = state.SelectedChatId.Value;
                _ = LoadConversationMessagesAsync(state.SelectedChatId.Value).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load window state, using defaults");
        }
    }

    /// <summary>
    /// Saves the current window state to disk.
    /// Avalonia's Window doesn't expose Left/Top, so we only save size and active tab.
    /// </summary>
    private async Task SaveWindowStateAsync()
    {
        if (_windowSettings == null) return;

        try
        {
            var state = new WindowStateSettings
            {
                Width = Width,
                Height = Height,
                ActiveTab = _activeTab,
                SelectedChatId = _selectedChatId
            };

            await _windowSettings.SaveAsync(state);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save window state");
        }
    }

    // ---- Cleanup on window close ----

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Save window state before closing
        _ = SaveWindowStateAsync();

        // Unsubscribe from server state changes before the window is closed
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged -= OnServerStateChanged;

        // Dispose context manager if it implements IDisposable
        _contextManager?.Dispose();
    }

    // ---- Game Menu Handlers (no-op — games removed) ----

    private void OnOpenModelClicked(object? sender, RoutedEventArgs e)
    {
        // Open model file dialog using Avalonia's modern StorageProvider API
        _ = Task.Run(async () =>
        {
            var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Model",
                AllowMultiple = false
            });
            if (files.Any())
            {
                _logger?.LogInformation("User selected model: {Path}", files.First().Path.ToString());
            }
        });
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    // Games removed — handlers kept for compatibility
    private void OnOpenMinesweeperClicked(object? sender, RoutedEventArgs e) { }
    private void OnOpenTetrisClicked(object? sender, RoutedEventArgs e) { }
    private void OnOpenSnakeClicked(object? sender, RoutedEventArgs e) { }
    private void OnOpenJezzballClicked(object? sender, RoutedEventArgs e) { }
    private void OnOpenSolitaireClicked(object? sender, RoutedEventArgs e) { }
}
