// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents
// Brought to you by Carls' Jr.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Application.Types.Agent;
using OpenLMStudio.Application.Services.Agent;
using OpenLMStudio.Domain.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Desktop.Controls;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for MainWindow.axaml.
/// Manages the main application window including chat list, conversation display, server controls, and tab navigation.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow>? _logger;
    private readonly IConversationManager? _conversationManager;
    private readonly IServerService? _serverService;
    private readonly IModelRepository? _modelRepository;
    private readonly IChatCompletionService? _chatCompletionService;

    private readonly IChatContextManager? _contextManager;
    private readonly IContextWindowBudgeter? _budgeter;
    private readonly IPinguStore? _pinguStore;
    private readonly IWindowSettings? _windowSettings;
    private readonly Infrastructure.Services.MainAIManager? _mainAIManager;
    private readonly Infrastructure.Services.SystemAIManager? _systemAIManager;
    private readonly IAgentToolExecutor? _agentToolExecutor;

    /// <summary>Flag to prevent duplicate title saves when both LostFocus and overlay click fire.</summary>
    private bool _titleEditSaving = false;
    /// <summary>Currently visible ToolCallForm popup (or null if closed).</summary>
    private ToolCallForm? _activeToolCallForm;

    /// <summary>Pingu avatar control for the bottom-right corner of the main window.</summary>
    private PinguAvatar? _pinguAvatar;

    /// <summary>Flag indicating whether a streaming response is in progress.</summary>
    private bool _isStreaming = false;

    /// <summary>Current send target ("MainAI" or "Pingu").</summary>
    private string _sendTarget = "MainAI";

    /// <summary>The current assistant message border being streamed into during an active SSE session.</summary>
    private Border? _currentAssistantBorder;

    /// <summary>The text block within the assistant message that receives streamed tokens.</summary>
    private TextBlock? _assistantTextBlock;

    /// <summary>Resolves the diffusion pipeline service for image generation (lazy from DI).</summary>
    private Application.Interfaces.IDiffusionPipelineService? _diffusionPipeline;

    // Tab tracking
    private string _activeTab = "Chat";
    private Guid? _selectedChatId;

    public MainWindow(
        ILogger<MainWindow>? logger,
        IConversationManager? conversationManager = null,
        IServerService? serverService = null,
        IModelRepository? modelRepository = null,
        IChatCompletionService? chatCompletionService = null,
        IChatContextManager? contextManager = null,
        IContextWindowBudgeter? budgeter = null,
        IPinguStore? pinguStore = null,
        IWindowSettings? windowSettings = null,
        Infrastructure.Services.MainAIManager? mainAIManager = null,
        Infrastructure.Services.SystemAIManager? systemAIManager = null,
        IAgentToolExecutor? agentToolExecutor = null)
    {
        InitializeComponent();
        _logger = logger;
        _mainAIManager = mainAIManager ?? ResolveMainAIManagerFromAppServices();
        _systemAIManager = systemAIManager ?? ResolveSystemAIManagerFromAppServices();
        _agentToolExecutor = agentToolExecutor ?? ResolveAgentToolExecutorFromAppServices();

        // Wire up model selector controls
        WireUpModelSelectors();

        // Register this window with static services (TabService, PanelService)
        TabService.SetWindow(this);
        PanelService.SetWindow(this);

        // Set window title programmatically to avoid XAML entity reference issues with "&" character
        this.Title = "OpenLMStudio - Local LLM Server & Chat Client";

        // Register keyboard shortcuts immediately after init so they fire regardless of focus.
        this.KeyDown += OnMainWindowKeyDown;

        // Use pre-resolved dependencies from App.OnStartup — if none are provided (for testing), fall back to DI resolution attempt.
        _conversationManager = conversationManager ?? ResolveConversationManagerFromAppServices();
        _serverService = serverService ?? ResolveServerServiceFromAppServices();
        _modelRepository = modelRepository ?? ResolveModelRepositoryFromAppServices();
        _chatCompletionService = chatCompletionService ?? ResolveChatCompletionServiceFromAppServices();
        _contextManager = contextManager ?? ResolveContextManagerFromAppServices();
        _budgeter = budgeter ?? ResolveBudgeterFromAppServices();
        _pinguStore = pinguStore;
        _windowSettings = windowSettings ?? ResolveWindowSettingsFromAppServices();

        // Load saved window state and apply it
        _ = LoadWindowStateAsync();

        // Subscribe to server state changes
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged += OnServerStateChanged;

        // Set up event handlers for UI interactions
        SetupEventHandlers();

        // Load tab click handlers (they need access to this instance's ShowTab method)
        AttachTabClickHandlers();

        // Wire up ToggleButton click handlers for left sidebar tabs
        WireUpLeftTabClickHandlers();

        // Initialize Pingu avatar if IPinguStore is available
        InitializePingu();

        // Defer chat list loading until after window is shown to avoid freezing the UI
        this.Opened += OnMainWindowOpened;

        // Initialize git status bar
        RefreshGitStatusBar();
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        // Unsubscribe to avoid re-running
        this.Opened -= OnMainWindowOpened;
        RefreshChatListAsync();

        // Wire up keyboard shortcuts
        KeyboardService.Initialize(this);

        // Initialize Agent Mode to off with 0 turns (badge hidden)
        SetAgentTurns(0);
    }

    /// <summary>
    /// Initializes the Pingu avatar control and wires up state change events.
    /// </summary>
    private void InitializePingu()
    {
        try
        {
            var sp = GetAppServiceProvider();
            var pinguStore = sp?.GetService<IPinguStore>();
            if (pinguStore != null)
            {
                _pinguAvatar = new PinguAvatar(pinguStore);
                _pinguAvatar.PointerPressed += OnPinguAvatarClicked;

                // Try to find the main Grid in the visual tree and add Pingu as a child
                try
                {
                    var mainGrid = FindGridInVisualTree(this);
                    if (mainGrid != null)
                        mainGrid.Children.Add(_pinguAvatar);
                }
                catch
                {
                    // Ignore errors adding Pingu to the visual tree
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize Pingu avatar");
        }
    }

    private void OnPinguAvatarClicked(object? sender, PointerPressedEventArgs e)
    {
        // Toggle Pingu menu on click
        if (_pinguStore != null)
        {
            _ = _pinguStore.ToggleMenuAsync();
        }
    }

    private void SetupEventHandlers()
    {
        // New chat button
        if (NewChatButton != null)
            NewChatButton.Click += OnNewChatClicked;

        // Send message button
        if (SendButton != null)
            SendButton.Click += OnSendMessageClicked;


        // Server start/stop buttons - both left and right panels need handlers
        if (LeftServerStartStopButton != null)
            LeftServerStartStopButton.Click += OnServerStartStopClicked;

        // Handle Enter key in input box for sending messages
        if (MessageInputBox != null)
            MessageInputBox.KeyDown += OnMessageInputKeyDown;

        // Context tab custom context injection button
        if (InjectCustomContextBtn != null)
            InjectCustomContextBtn.Click += OnInjectCustomContextClicked;

        // Context compression selector (left sidebar)
        if (ContextCompressionSelector != null)
            ContextCompressionSelector.SelectionChanged += OnContextCompressionSelectionChanged;

        // Random seed button
        if (RandomSeedButton != null)
            RandomSeedButton.Click += OnRandomSeedClicked;


        // Agent mode toggle button
        if (AgentModeToggle != null)
            AgentModeToggle.Click += OnAgentModeToggleClicked;

        // Agent turns badge (clickable orange badge)
        if (AgentTurnsBadge != null)
            AgentTurnsBadge.Click += OnAgentTurnsBadgeClicked;

        // Plan/Act toggle
        if (PlanButton != null)
            PlanButton.Click += OnPlanClicked;
        if (ActButton != null)
            ActButton.Click += OnActClicked;

        // Safety toggles (WWW, Read, Edit, Exec)
        if (SafetyWWW != null)
            SafetyWWW.Click += OnSafetyToggleClicked;
        if (SafetyRead != null)
            SafetyRead.Click += OnSafetyToggleClicked;
        if (SafetyEdit != null)
            SafetyEdit.Click += OnSafetyToggleClicked;
        if (SafetyExec != null)
            SafetyExec.Click += OnSafetyToggleClicked;


        // Image generation generate button
        if (ImageGenGenerateBtn != null)
            ImageGenGenerateBtn.Click += OnImageGenGenerateClicked;

        // Image generation model selector
        if (ImageGenModelSelector != null)
            ImageGenModelSelector.SelectionChanged += OnImageGenModelSelectorSelectionChanged;

        // Settings button
        if (SettingsButton != null)
            SettingsButton.Click += OnSettingsClicked;

        // Git status bar
        if (GitStatusBorder != null)
            GitStatusBorder.PointerPressed += OnGitStatusClicked;

        // Tools button
        if (ToolsButton != null)
            ToolsButton.Click += OnToolsButtonClicked;

        // Close tool popup button
        if (CloseToolPopup != null)
            CloseToolPopup.Click += OnCloseToolPopupClicked;

    }

    /// <summary>
    /// Wires up the MainModelSelector and SystemModelSelector controls with their respective managers.
    /// </summary>
    private void WireUpModelSelectors()
    {
        // Wire up MainModelSelector control
        if (MainModelSelectorControl != null)
        {
            if (_mainAIManager != null)
                MainModelSelectorControl.SetManager(_mainAIManager);
        }

        // Wire up SystemModelSelector control
        if (SystemModelSelectorControl != null)
        {
            if (_systemAIManager != null)
                SystemModelSelectorControl.SetManager(_systemAIManager);
        }
    }

    /// <summary>
    /// Resolves MainAIManager from the application service provider.
    /// </summary>
    private Infrastructure.Services.MainAIManager ResolveMainAIManagerFromAppServices()
    {
        try
        {
            var sp = GetAppServiceProvider();
            var manager = sp?.GetService(typeof(Infrastructure.Services.MainAIManager)) as Infrastructure.Services.MainAIManager;
            return manager ?? sp?.GetService<Infrastructure.Services.MainAIManager>()
                ?? throw new InvalidOperationException("MainAIManager not registered in DI");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve MainAIManager from app services");
            throw;
        }
    }

    /// <summary>
    /// Resolves SystemAIManager from the application service provider.
    /// </summary>
    private Infrastructure.Services.SystemAIManager ResolveSystemAIManagerFromAppServices()
    {
        try
        {
            var sp = GetAppServiceProvider();
            var manager = sp?.GetService(typeof(Infrastructure.Services.SystemAIManager)) as Infrastructure.Services.SystemAIManager;
            return manager ?? sp?.GetService<Infrastructure.Services.SystemAIManager>()
                ?? throw new InvalidOperationException("SystemAIManager not registered in DI");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve SystemAIManager from app services");
            throw;
        }
    }

    /// <summary>
    /// Resolves AgentToolExecutor from the application service provider.
    /// </summary>
    private IAgentToolExecutor? ResolveAgentToolExecutorFromAppServices()
    {
        try
        {
            var sp = GetAppServiceProvider();
            return sp?.GetService(typeof(IAgentToolExecutor)) as IAgentToolExecutor
                ?? sp?.GetService<IAgentToolExecutor>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve AgentToolExecutor from app services");
            return null;
        }
    }

    /// <summary>
    /// Toggles Agent Mode on/off.
    /// When turned on, adds the configured number of turns.
    /// When turned off, resets turns to 0.
    /// </summary>
    private void OnAgentModeToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (AgentTurnsText == null)
            return;

        // Use badge visibility as the source of truth for whether Agent Mode is on
        var isOn = AgentTurnsBadge != null && AgentTurnsBadge.IsVisible;

        if (isOn)
        {
            // Turn off: reset turns to 0
            SetAgentTurns(0);
            // Set background to BgTertiary so :hover can override
            AgentModeToggle?.SetValue(Button.BackgroundProperty, (Avalonia.Media.ISolidColorBrush)(this.FindResource("BgTertiary") ?? Avalonia.Media.Brushes.Gray));
            AgentModeToggle?.Classes.Remove("active");
        }
        else
        {
            // Turn on: set to default turns
            var defaultTurns = GetAgentDefaultTurns();
            SetAgentTurns(defaultTurns);
            // Set background to AccentBlue so :hover will override to #1E88E5
            AgentModeToggle?.SetValue(Button.BackgroundProperty, (Avalonia.Media.ISolidColorBrush)(this.FindResource("AccentBlue") ?? Avalonia.Media.Brushes.White));
            AgentModeToggle?.Classes.Add("active");
        }
    }

    /// <summary>
    /// Increments the agent turns count by 2.
    /// </summary>
    private void OnAgentTurnsBadgeClicked(object? sender, RoutedEventArgs e)
    {
        if (AgentTurnsText == null)
            return;

        var currentTurns = ParseInt(AgentTurnsText.Text, 0);
        SetAgentTurns(currentTurns + 2);
    }

    /// <summary>
    /// Sets the agent turns count to the specified value.
    /// Hides the badge when turns is 0.
    /// </summary>
    private void SetAgentTurns(int turns)
    {
        if (AgentTurnsText == null)
            return;

        AgentTurnsText.Text = turns.ToString();

        // Show/hide the badge based on turns
        if (AgentTurnsBadge != null)
        {
            AgentTurnsBadge.IsVisible = turns > 0;

            if (turns > 0)
            {
                AgentTurnsBadge.SetCurrentValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, (Avalonia.Media.ISolidColorBrush)(this.FindResource("AccentOrange") ?? Avalonia.Media.Brushes.Orange));
                AgentTurnsBadge.SetCurrentValue(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, Avalonia.Media.Brushes.White);
            }
        }

        _logger?.LogDebug("Agent turns set to {Turns}", turns);
    }

    /// <summary>
    /// Reads the AgentDefaultTurns setting from the settings service.
    /// </summary>
    private int GetAgentDefaultTurns()
    {
        try
        {
            var sp = GetAppServiceProvider();
            var svc = sp?.GetService(typeof(IWindowSettings)) as IWindowSettings;
            if (svc != null)
            {
                // IWindowSettings is a save/load interface; for now we use the default value
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read AgentDefaultTurns from settings");
        }

        return 10;
    }

    /// <summary>
    /// Parses an integer from a string, returning a default value on failure.
    /// </summary>
    private static int ParseInt(string? value, int defaultValue)
    {
        if (int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }


    /// <summary>
    /// Initializes the status bar with the current git commit hash and wires up click handler.
    /// </summary>
    private void RefreshGitStatusBar()
    {
        try
        {
            var shortHash = GetGitCommitShort();
            GitStatusText!.Text = $"OpenLMStudio {shortHash}";

            // Load full log for popup
            var log = GetGitLog(7);
            var sb = new StringBuilder();
            sb.Append($"OpenLMStudio {GitInfo.FullName}");
            if (!string.Equals(GitInfo.Dirty, "true", StringComparison.OrdinalIgnoreCase))
                sb.Append(" (clean)");
            sb.AppendLine();
            sb.AppendLine("Recent commits:");
            foreach (var line in log.Split('\n').Where(l => l.Trim().Length > 0))
                sb.AppendLine(line.Trim());
            GitLogContent!.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load git status");
        }
    }

    private void OnGitStatusClicked(object? sender, PointerPressedEventArgs e)
    {
        GitLogPopup?.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, !GitLogPopup.IsOpen);
    }

    /// <summary>
    /// Deletes the currently selected chat from the UI, backing store, and file system.
    /// </summary>
    private async void OnDeleteChatClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedChatId == null || _conversationManager == null)
            return;

        var chatId = _selectedChatId.Value;
        _logger?.LogInformation("Deleting chat {ChatId}", chatId);

        // Delete from conversation manager (removes from memory and deletes the associated file)
        try
        {
            await _conversationManager.DeleteChatAsync(chatId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete chat {ChatId}", chatId);
            return;
        }

        // Clear the selected chat
        _selectedChatId = null;

        // Hide the delete button
        DeleteChatButton?.SetValue(Button.IsVisibleProperty, false);

        // Refresh the chat list
        RefreshChatListAsync();

        // Clear the message display
        MessageDisplayPanel?.Children.Clear();
        MessageDisplayPanel?.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new TextBlock
            {
                Text = "No messages yet. Start a conversation!",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 14
            }
        });
    }

    /// <summary>
    /// Toggles the chat title between display mode and edit mode.
    /// Uses Task.Run to avoid blocking the UI thread (prevents freeze).
    /// </summary>
    private void OnChatTitleClicked(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (_selectedChatId == null)
            {
                ChatTitleDisplay.Text = "New Chat";
                return;
            }

            if (ChatTitleDisplay == null || ChatTitleEdit == null)
                return;

            var chatId = _selectedChatId.Value;
            var mgr = _conversationManager;

            // Run the blocking call on a background thread to avoid freezing the UI
            Task.Run(async () =>
            {
                var chat = mgr != null
                    ? await mgr.LoadChatAsync(chatId).ConfigureAwait(false)
                    : null;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (chat != null && !string.IsNullOrEmpty(chat.Name))
                    {
                        ChatTitleDisplay.Text = chat.Name;
                        ChatTitleEdit.Text = chat.Name;
                    }
                    else
                    {
                        ChatTitleDisplay.Text = "New Chat";
                        ChatTitleEdit.Text = "New Chat";
                    }

                    // Switch to edit mode and show overlay
                    ChatTitleDisplay.IsVisible = false;
                    ChatTitleEdit.IsVisible = true;
                    TitleEditOverlay.IsVisible = true;
                    // Use SelectAll() instead of Focus() to avoid blocking the dispatcher thread
                    ChatTitleEdit.SelectAll();
                });
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load chat title for {ChatId}", _selectedChatId);
            ChatTitleDisplay.Text = "New Chat";
            ChatTitleEdit.Text = "New Chat";
        }
    }

    /// <summary>
    /// Saves the chat title and exits edit mode when focus is lost.
    /// Uses a flag to prevent duplicate saves when both LostFocus and overlay click fire.
    /// </summary>
    private void OnChatTitleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (ChatTitleEdit == null || ChatTitleDisplay == null)
            return;

        // If we're already in the process of saving (triggered by overlay click), skip
        if (_titleEditSaving)
            return;

        // Switch mode immediately (before the save completes)
        // This ensures that if the user clicks another control, the edit mode
        // is already switched and subsequent clicks work correctly
        var wasVisible = ChatTitleEdit.IsVisible;
        ChatTitleEdit.IsVisible = false;
        ChatTitleDisplay.IsVisible = true;

        // Only save if the title was visible (i.e., we were actually in edit mode)
        if (wasVisible)
        {
            _ = SaveChatTitleAsync(refreshList: true);
        }
    }

    /// <summary>
    /// Saves the chat title when Enter is pressed.
    /// </summary>
    private async void OnChatTitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SaveChatTitleAsync(refreshList: true);
            ChatTitleEdit.IsVisible = false;
            ChatTitleDisplay.IsVisible = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Cancel edit - restore original title
            if (_selectedChatId != null)
            {
                try
                {
                    var chat = _conversationManager != null
                        ? await _conversationManager.LoadChatAsync(_selectedChatId.Value)
                        : null;
                    ChatTitleDisplay.Text = chat?.Name ?? "New Chat";
                }
                catch
                {
                    ChatTitleDisplay.Text = "New Chat";
                }
            }
            ChatTitleEdit.IsVisible = false;
            ChatTitleDisplay.IsVisible = true;
        }
    }

    /// <summary>
    /// Called when ChatTitleEdit loses focus while the window is not focused.
    /// </summary>
    private void OnChatTitleEditLostFocusWhileNotFocused(object? sender, RoutedEventArgs e)
    {
        // Always save and exit when this fires
        OnChatTitleLostFocus(sender, e);
    }

    // =========================================================================
    // Status bar button handlers
    // =========================================================================

    /// <summary>
    /// Opens the OpenLMStudio data folder (AppData directory) in the native file explorer.
    /// </summary>
    private void OnStatusFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Try to get the data folder from the DI container (AppDataDirectoryResolver)
            var sp = GetAppServiceProvider();
            var resolver = sp?.GetService<Infrastructure.Services.AppDataDirectoryResolver>();
            var dataDir = resolver?.GetAppDataDirectory() ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var psi = new ProcessStartInfo
            {
                FileName = dataDir,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open folder");
            ShowError($"Failed to open folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Wipes the OpenLMStudio root folder (except models/*.gguf) and restarts the app.
    /// </summary>
    private void OnStatusResetClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var appDir = AppContext.BaseDirectory;
            var rootDir = Directory.GetParent(appDir)?.Parent?.Parent?.FullName ?? appDir;

            // Collect files to delete (everything except models/*.gguf)
            var filesToDelete = new List<string>();
            foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(rootDir, file);
                // Keep models/*.gguf files
                if (relativePath.StartsWith("models" + Path.DirectorySeparatorChar) &&
                    string.Equals(Path.GetExtension(file), ".gguf", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                // Keep the app's own executable and DLLs
                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                filesToDelete.Add(file);
            }

            // Delete collected files
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete {File}", file);
                }
            }

            // Delete empty directories
            foreach (var dir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories).Reverse())
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    try { Directory.Delete(dir); } catch { /* ignore */ }
                }
            }

            // Restart the app
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            };
            Process.Start(psi);

            // Close the current instance
            if (global::Avalonia.Application.Current is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reset app");
            ShowError($"Failed to reset app: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when the transparent overlay is clicked while the title is being edited.
    /// This ensures the title saves even when focus doesn't propagate properly.
    /// </summary>
    private void OnTitleEditOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // If already saving, don't duplicate
        if (_titleEditSaving)
            return;

        _titleEditSaving = true;

        // If the edit box is still visible, save and exit edit mode
        if (ChatTitleEdit != null && ChatTitleEdit.IsVisible)
        {
            var title = ChatTitleEdit.Text?.Trim();
            if (!string.IsNullOrEmpty(title) && _selectedChatId != null)
            {
                _ = SaveChatTitleAsync(refreshList: true);
            }

            // Hide edit mode
            ChatTitleEdit.IsVisible = false;
            ChatTitleDisplay.IsVisible = true;
            TitleEditOverlay.IsVisible = false;

            // Reset the flag after a short delay to let the LostFocus event settle
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _titleEditSaving = false;
            });
        }
        else
        {
            _titleEditSaving = false;
        }
    }

    /// <summary>
    /// Saves the current chat title to the conversation manager.
    /// </summary>
    private async Task SaveChatTitleAsync(bool refreshList = false)
    {
        if (_selectedChatId == null || ChatTitleEdit == null || _conversationManager == null)
            return;

        var newTitle = ChatTitleEdit.Text?.Trim();
        if (string.IsNullOrEmpty(newTitle))
            return;

        try
        {
            await _conversationManager.RenameChatAsync(_selectedChatId.Value, newTitle);
            ChatTitleDisplay.Text = newTitle;

            // Refresh the chat list to update the name in the left sidebar
            if (refreshList)
            {
                await Dispatcher.UIThread.InvokeAsync(() => RefreshChatListAsync());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rename chat {ChatId}", _selectedChatId.Value);
        }
    }

    private static string GetGitCommitShort()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo) ?? throw new InvalidOperationException();
            var result = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return result.Length > 0 ? result : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetGitLog(int count)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log -{count} --oneline --date=short --format=\"%h %ad %s\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var proc = Process.Start(startInfo) ?? throw new InvalidOperationException();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Wires up ToggleButton click handlers for the left sidebar tabs.
    /// </summary>
    private void WireUpLeftTabClickHandlers()
    {
        var tabs = new (ToggleButton button, string tabName)[]
        {
            (ChatTab, "Chat"),
            (ServerTab, "Server"),
            (ModelsTab, "Models"),
            (DevicesTab, "Devices"),
            (ContextTab, "Context"),
            (PinguTab, "Pingu"),
            (ImageGenTab, "ImageGen"),
        };

        foreach (var (button, tabName) in tabs)
        {
            if (button != null)
            {
                button.Click += (sender, e) =>
                {
                    if (tabName == "Context")
                        UpdateRightSidebarTab("Context");
                    ShowTab(tabName);
                };
            }
        }
    }

    // =========================================================================
    // Plan/Act toggle and Safety handlers
    // =========================================================================

    /// <summary>
    /// Toggles between Plan and Act modes.
    /// Modes are mutually exclusive — enabling one disables the other.
    /// </summary>
    private void OnPlanClicked(object? sender, RoutedEventArgs e)
    {
        PlanButton?.Classes.Add("active");
        ActButton?.Classes.Remove("active");
        _logger?.LogInformation("Plan/Act mode toggled to: Plan");
    }

    private void OnActClicked(object? sender, RoutedEventArgs e)
    {
        ActButton?.Classes.Add("active");
        PlanButton?.Classes.Remove("active");
        _logger?.LogInformation("Plan/Act mode toggled to: Act");
    }

    /// <summary>
    /// Handles clicks on the safety toggles.
    /// Read is a prerequisite: Exec or Edit cannot be on without Read.
    /// </summary>
    private void OnSafetyToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clickedButton)
        {
            var isChecked = clickedButton.IsChecked == true;
            var name = clickedButton.Name;

            if (isChecked)
            {
                // Exec enables Read
                if (name == nameof(SafetyExec) && SafetyRead != null)
                    SafetyRead.SetValue(ToggleButton.IsCheckedProperty, true);

                // Edit enables Read
                if (name == nameof(SafetyEdit) && SafetyRead != null)
                    SafetyRead.SetValue(ToggleButton.IsCheckedProperty, true);
            }
            else
            {
                // Read disabled: disable Exec and Edit as well
                if (name == nameof(SafetyRead))
                {
                    if (SafetyExec != null)
                        SafetyExec.SetValue(ToggleButton.IsCheckedProperty, false);
                    if (SafetyEdit != null)
                        SafetyEdit.SetValue(ToggleButton.IsCheckedProperty, false);
                }

                // Exec disabled: disable Read as well
                if (name == nameof(SafetyExec) && SafetyRead != null)
                    SafetyRead.SetValue(ToggleButton.IsCheckedProperty, false);

                // Edit disabled: disable Read as well
                if (name == nameof(SafetyEdit) && SafetyRead != null)
                    SafetyRead.SetValue(ToggleButton.IsCheckedProperty, false);
            }

            _logger?.LogDebug("Safety toggle {Name} set to {State}", name, isChecked);
        }
    }

    /// <summary>
    /// Handles clicks on the Send target ContextMenuItem (MainAI / Pingu).
    /// Updates the Send button content to show the selected target.
    /// </summary>
    private void OnSendTargetSelected(object? sender, RoutedEventArgs e)
    {
        var target = "MainAI";
        if (sender is ContentControl item)
        {
            // Check Tag property
            var tag = item.Tag as string;
            if (!string.IsNullOrEmpty(tag))
                target = tag;

            // Also check Content if Tag is not set
            if (string.IsNullOrEmpty(target) && item.Content is string content)
                target = content;
        }

        _sendTarget = target;
        SendButton?.SetValue(ContentControl.ContentProperty, $"Send ({target})");
        _logger?.LogInformation("Send target changed to: {Target}", target);
    }

    // =========================================================================
    // Tool Call Form popup
    // =========================================================================

    /// <summary>
    /// Handles clicks on the Tools button — opens the tool call popup.
    /// </summary>
    private void OnToolsButtonClicked(object? sender, RoutedEventArgs e)
    {
        // Resolve AgentToolExecutor from DI if not already resolved
        if (_agentToolExecutor == null)
        {
            _logger?.LogWarning("AgentToolExecutor not resolved — cannot open tool call popup");
            return;
        }

        // Populate the tool list if empty
        if (ToolListPanel != null && ToolListPanel.Children.Count == 0)
        {
            PopulateToolList();
        }

        // Show the popup
        if (ToolCallPopup != null)
            ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, true);
    }

    /// <summary>
    /// Populates the ToolListPanel with buttons for each available tool.
    /// </summary>
    private void PopulateToolList()
    {
        if (ToolListPanel == null)
            return;

        var executor = _agentToolExecutor as AgentToolExecutor;
        if (executor == null)
            return;

        var tools = executor.ListAvailableTools();
        ToolListPanel.Children.Clear();

        foreach (var tool in tools.OrderBy(t => t.Name))
        {
            var btn = new Button
            {
                Content = $"{GetToolIcon(tool.Name)}  {tool.Name}",
                Background = (SolidColorBrush)(this.FindResource("BgTertiary") ?? Avalonia.Media.Brushes.Gray),
                Foreground = (SolidColorBrush)(this.FindResource("TextPrimary") ?? Avalonia.Media.Brushes.Gray),
                Padding = new Thickness(10, 6),
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                FontSize = 12
            };

            btn.AddHandler(Button.PointerEnteredEvent, (s, ev) =>
            {
                if (btn.Background is Avalonia.Media.ISolidColorBrush sb)
                {
                    // Keep the existing background style
                }
            });

            btn.Click += (s, ev) =>
            {
                ShowToolCallForm(tool, executor);
            };

            ToolListPanel.Children.Add(btn);
        }
    }

    /// <summary>
    /// Shows the ToolCallForm popup for the given tool.
    /// </summary>
    private void ShowToolCallForm(ToolDefinition tool, AgentToolExecutor executor)
    {
        // Close the tool list popup
        if (ToolCallPopup != null)
            ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);

        // Dispose any existing ToolCallForm
        if (_activeToolCallForm != null)
        {
            _activeToolCallForm.Dispose();
        }

        // Create and show the form
        _activeToolCallForm = new ToolCallForm(tool, executor, OnToolCallFormCancel);

        // Position the form popup below the Tools button
        var formPopup = new Popup
        {
            PlacementTarget = ToolsButton,
            Placement = PlacementMode.Bottom,
            IsOpen = true,
            Child = _activeToolCallForm,
            HorizontalOffset = 0,
            VerticalOffset = 0
        };

        formPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, true);
        _activeToolCallForm.PopupReference = formPopup;
        _activeToolCallForm.Cancelled += () =>
        {
            formPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
            _activeToolCallForm = null;
        };
    }

    /// <summary>
    /// Called when the ToolCallForm is cancelled.
    /// </summary>
    private void OnToolCallFormCancel(ToolCallForm form)
    {
        _activeToolCallForm = null;
    }

    /// <summary>
    /// Closes the tool call popup.
    /// </summary>
    private void OnCloseToolPopupClicked(object? sender, RoutedEventArgs e)
    {
        if (ToolCallPopup != null)
            ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
    }

    /// <summary>
    /// Returns the emoji icon for a tool name.
    /// </summary>
    private static string GetToolIcon(string toolName)
    {
        return toolName switch
        {
            "write_to_file" => "📝",
            "replace_in_file" => "✏️",
            "read_file" => "📄",
            "search_files" => "🔍",
            "list_files" => "📋",
            "execute_command" => "⚡",
            "browser_action" => "🌐",
            "use_mcp_tool" => "🔗",
            "access_mcp_resource" => "📡",
            "load_mcp_documentation" => "📚",
            "plan_mode_respond" => "💭",
            "act_mode_respond" => "🚀",
            "attempt_completion" => "✅",
            "new_task" => "🆕",
            "use_skill" => "🛠️",
            "use_subagents" => "🤖",
            "apply_patch" => "🩹",
            "generate_explanation" => "🔎",
            "web_fetch" => "🌍",
            "web_search" => "🔎",
            "ask_followup_question" => "❓",
            _ => "🔧"
        };
    }

}

// =========================================================================
// Keyboard shortcuts service
// =========================================================================
/// <summary>
/// Global keyboard shortcuts for the main window.
/// </summary>
public static class KeyboardService
{
    private static MainWindow? _mainWindow;

    public static void Initialize(MainWindow window)
    {
        _mainWindow = window;
        window.KeyDown += OnMainWindowKeyDown;
    }

    private static void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+N: New Chat
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.N)
        {
            _mainWindow?.NewChatButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+S: Toggle Server
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
            e.Key == Key.S)
        {
            _mainWindow?.LeftServerStartStopButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
            return;
        }

        // Ctrl+Enter: Send message (also handled by MessageInputBox.KeyDown, but also global)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Return)
        {
            _mainWindow?.SendButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
            return;
        }

        // Ctrl+Tab / Ctrl+Shift+Tab: Cycle tabs
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Tab)
        {
            var direction = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
            KeyboardService.CycleTab(direction);
            e.Handled = true;
            return;
        }

        // Escape: Close popups and reset focus
        if (e.Key == Key.Escape)
        {
            _mainWindow?.GitLogPopup?.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
            e.Handled = true;
            return;
        }
    }

    /// <summary>
    /// Cycles the active tab by the given direction (+1 for forward, -1 for backward).
    /// Uses Avalonia visual tree to find TabControls.
    /// </summary>
    public static void CycleTab(int direction)
    {
        if (_mainWindow == null) return;

        // Search all TabControls in the visual tree
        var allTabs = _mainWindow.GetVisualDescendants()
            .OfType<TabControl>()
            .SelectMany(tc => tc.Items.Cast<TabItem>())
            .Where(t => t.IsSelected)
            .ToList();

        if (allTabs.Count == 1)
        {
            // Find the TabControl that contains the selected item
            var selected = allTabs[0];
            var parent = selected.GetVisualParent<TabControl>();
            if (parent == null) return;

            var items = parent.Items.Cast<TabItem>().ToList();
            var index = items.IndexOf(selected);
            if (index < 0) return;

            var nextIndex = (index + direction + items.Count) % items.Count;
            items[nextIndex].IsSelected = true;
        }
    }
}
