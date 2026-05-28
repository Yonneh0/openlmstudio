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
using OpenLMStudio.Desktop;

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
    private IAgentToolExecutor? _agentToolExecutor;

    /// <summary>Flag to prevent duplicate title saves when both LostFocus and overlay click fire.</summary>
    private int _titleEditSaving = 0;
    /// <summary>Currently visible ToolCallForm popup (or null if closed).</summary>
    private ToolCallForm? _activeToolCallForm;

    /// <summary>Pingu avatar control for the bottom-right corner of the main window.</summary>
    private PinguAvatar? _pinguAvatar;

    /// <summary>PinguCanvas control for GPU rendering.</summary>
    private PinguCanvas? _pinguCanvas;

    /// <summary>DispatcherTimer for the render loop (60fps).</summary>
    private DispatcherTimer? _pinguRenderTimer;

    /// <summary>Flag indicating whether a streaming response is in progress.</summary>
    private volatile bool _isStreaming = false;

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

    /// <summary>
    /// Gets the currently active tab name.
    /// </summary>
    public string ActiveTab => _activeTab;

    private Guid? _selectedChatId;

    /// <summary>
    /// Parsed git log entries for the GitLogTable popup.
    /// </summary>
    public IEnumerable<Controls.GitLogEntry> GitLogEntries { get; private set; } = Array.Empty<Controls.GitLogEntry>();

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions _jsonFormatOptions = new()
    {
        WriteIndented = true
    };

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

        // Keyboard shortcuts are registered via KeyboardService.Initialize() in OnMainWindowOpened
        // to avoid duplicate registration (the constructor registration and KeyboardService would both
        // fire OnMainWindowKeyDown, causing double execution of shortcuts)

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

        // Wire up ToggleButton click handlers for left sidebar tabs
        WireUpLeftTabClickHandlers();

        // Wire up right sidebar tab toggles
        WireUpRightSidebarTabs();

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
    /// Initializes the Pingu avatar control and GPU canvas in the bottom-right corner.
    /// </summary>
    private void InitializePingu()
    {
        if (_pinguStore != null && _pinguAvatar == null)
        {
            _pinguAvatar = new PinguAvatar(_pinguStore);
            // Find a suitable panel to host the avatar (e.g., a Border/Canvas in the XAML)
            // Try PinguCornerPanel first, fall back to the window's content
            var target = this.FindControl<Canvas>("PinguCornerPanel");
            if (target != null)
                target.Children.Add(_pinguAvatar);
            // If no panel found, the PinguCornerPanel will be defined in XAML
        }

        // Initialize the GPU canvas
        InitializePinguCanvas();
    }

    /// <summary>
    /// Initializes the PinguCanvas control and starts the render loop.
    /// </summary>
    private void InitializePinguCanvas()
    {
        // Create the PinguCanvas
        _pinguCanvas = new PinguCanvas
        {
            Width = 350,
            Height = 350,
            Opacity = 0.95,
            ZIndex = 10
        };

        // Use PinguCornerPanel (which is already positioned in the bottom-right corner of the window)
        // to host the canvas so it doesn't interfere with the center pane content
        var targetPanel = this.FindControl<Canvas>("PinguCornerPanel");
        if (targetPanel != null)
        {
            // Set the Canvas size to match the CenterPaneGrid
            targetPanel.Width = CenterPaneGrid.Width - 400; // Leave room for right sidebar
            targetPanel.Height = CenterPaneGrid.Height - 100; // Leave room for status bar
            _pinguCanvas.Width = 350;
            _pinguCanvas.Height = 350;

            // Add the canvas directly to PinguCornerPanel (preserve any existing children like PinguAvatar)
            targetPanel.Children.Add(_pinguCanvas);

            // Position it in the bottom-right of the panel
            Canvas.SetRight(_pinguCanvas, 10);
            Canvas.SetBottom(_pinguCanvas, 10);
        }

        // Initialize the renderer (this will load/create mesh, texture, etc.)
        if (_pinguStore != null)
        {
            try
            {
                var renderFunc = _pinguStore.CreateRenderer();
                _pinguCanvas.Initialize(renderFunc);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to initialize PinguCanvas renderer");
            }
        }

        // Start the render loop
        StartPinguRenderLoop();

        // Wire up cursor tracking (placeholder — no-op handler)
        this.PointerMoved += OnMainWindowPointerMoved;
    }

    /// <summary>
    /// Handles pointer movement on the main window — used for Pingu cursor tracking.
    /// </summary>
    private void OnMainWindowPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // Update Pingu cursor position (no-op handler — cursor tracking to be implemented)
    }

    /// <summary>
    /// Starts the render loop using a DispatcherTimer at 60fps.
    /// </summary>
    private void StartPinguRenderLoop()
    {
        _pinguRenderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16.67) // ~60fps
        };
        _pinguRenderTimer.Tick += OnPinguRenderTick;
        _pinguRenderTimer.Start();
    }

    /// <summary>
    /// Handles the render loop tick — triggers a render update.
    /// </summary>
    private void OnPinguRenderTick(object? sender, EventArgs e)
    {
        _pinguCanvas?.InvalidateRender();
    }

    /// <summary>
    /// Handles pointer pressed on the PinguHomeTile.
    /// </summary>
    private void OnPinguHomeTileClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Toggle Pingu home tile visibility
        PinguHomeTile?.SetValue(Avalonia.Controls.Primitives.TemplatedControl.IsVisibleProperty, !PinguHomeTile.IsVisible);
    }

    /// <summary>
    /// Handles pointer pressed on the PinguCornerPanel.
    /// </summary>
    private void OnPinguCornerPanelPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Toggle Pingu panel visibility
        if (_pinguAvatar != null)
        {
            _pinguAvatar.IsVisible = !_pinguAvatar.IsVisible;
        }
    }

    private void SetupEventHandlers()
    {
        // New chat button
        if (NewChatButton != null)
            NewChatButton.Click += OnNewChatClicked;

        // NOTE: SendButton.Click is wired directly in XAML (Click="OnSendMessageClicked"), do NOT register here to avoid double-firing


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

        // Git status bar
        if (GitStatusBorder != null)
            GitStatusBorder.PointerPressed += OnGitStatusClicked;

        // Tools button
        if (ToolsButton != null)
            ToolsButton.Click += OnToolsButtonClicked;

        // Close tool popup button
        if (CloseToolPopup != null)
            CloseToolPopup.Click += OnCloseToolPopupClicked;

        // Handle clicks on the center pane to close the popup when clicking outside
        if (CenterPaneGrid != null)
            CenterPaneGrid.PointerPressed += OnCenterPanePointerPressed;

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

        // Use turns count as the source of truth for whether Agent Mode is on
        var currentTurns = ParseInt(AgentTurnsText.Text, 0);
        var isOn = currentTurns > 0;

        if (isOn)
        {
            // Turn off: reset turns to 0
            SetAgentTurns(0);
            // Remove active class — XAML style will restore default background
            AgentModeToggle?.Classes.Remove("active");
        }
        else
        {
            // Turn on: set to default turns
            var defaultTurns = GetAgentDefaultTurns();
            SetAgentTurns(defaultTurns);
            // Add active class — XAML style will set AccentBlue background
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
    /// Parses the git log string from GitInfo into GitLogEntry objects and updates the table.
    /// </summary>
    private void RefreshGitStatusBar()
    {
        try
        {
            GitStatusText!.Text = $"OpenLMStudio {GitInfo.FullName}";

            var entries = ParseGitLog(GitInfo.Log);
            GitLogEntries = entries;

            // Update the GitLogTableControl if it exists
            if (GitLogTableControl != null)
            {
                GitLogTableControl.SetValue(Controls.GitLogTable.EntriesProperty, entries);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load git status");
        }
    }

    /// <summary>
    /// Parses the semicolon-delimited git log string into a list of GitLogEntry objects.
    /// Format: "commit HASH;Author: NAME;    MESSAGE;commit HASH;..."
    /// </summary>
    private static List<Controls.GitLogEntry> ParseGitLog(string log)
    {
        var entries = new List<Controls.GitLogEntry>();
        if (string.IsNullOrEmpty(log))
            return entries;

        // Split by "commit " to get individual entries
        var parts = log.Split(new[] { "commit " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Extract hash (first token)
            var hashEnd = trimmed.IndexOf(';');
            var hash = hashEnd > 0 ? trimmed.Substring(0, hashEnd) : trimmed;

            // Extract author
            var authorStart = trimmed.IndexOf("Author: ");
            var authorEnd = trimmed.IndexOf(";", authorStart > 0 ? authorStart : 0);
            var author = authorStart > 0 && authorEnd > authorStart
                ? trimmed.Substring(authorStart + 8, authorEnd - authorStart - 8)
                : "Unknown";

            // Extract message (everything after "Author: NAME;    ")
            var messageStart = trimmed.IndexOf(";", authorEnd + 1);
            var message = messageStart >= 0
                ? trimmed.Substring(messageStart + 1).TrimStart()
                : trimmed;

            entries.Add(new Controls.GitLogEntry(hash, author, message));
        }

        return entries;
    }

    private void OnGitStatusClicked(object? sender, PointerPressedEventArgs e)
    {
        GitLogPopup?.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, !GitLogPopup.IsOpen);
    }

    /// <summary>
    /// Closes the GitLog popup when clicking outside of it.
    /// </summary>
    private void OnGitLogOverlayClicked(object? sender, PointerPressedEventArgs e)
    {
        if (GitLogPopup != null && GitLogPopup.IsOpen)
        {
            // Check if the click was outside the popup content
            var position = e.GetPosition(GitLogPopup.Child);
            if (position.X >= 0 && position.X <= GitLogPopup.Width &&
                position.Y >= 0 && position.Y <= GitLogPopup.Height)
            {
                // Click was inside the popup, don't close
                return;
            }
            GitLogPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
        }
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
    /// Sets _titleEditSaving = true BEFORE the async operation to prevent race conditions.
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

            // Set the flag BEFORE starting the async operation to prevent race conditions
            System.Threading.Interlocked.Exchange(ref _titleEditSaving, 1);

            var chatId = _selectedChatId.Value;
            var mgr = _conversationManager;

            // Run the blocking call on a background thread to avoid freezing the UI
            Task.Run(async () =>
            {
                var chat = mgr != null
                    ? await mgr.LoadChatAsync(chatId)
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
        if (System.Threading.Interlocked.CompareExchange(ref _titleEditSaving, 1, 0) != 0)
            return;

        // Only save if the title was visible (i.e., we were actually in edit mode)
        var wasVisible = ChatTitleEdit.IsVisible;
        if (!wasVisible)
            return;

        // Switch mode immediately (before the save completes)
        // This ensures that if the user clicks another control, the edit mode
        // is already switched and subsequent clicks work correctly
        ChatTitleEdit.IsVisible = false;
        ChatTitleDisplay.IsVisible = true;

        _ = SaveChatTitleAsync(refreshList: true);
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
            // Reset _titleEditSaving so subsequent clicks work correctly
            System.Threading.Interlocked.Exchange(ref _titleEditSaving, 0);
            ChatTitleEdit.IsVisible = false;
            ChatTitleDisplay.IsVisible = true;
        }
    }

    /// <summary>
    /// Wipes the AppData settings directory (C:\Users\Yonneh\AppData\Roaming\OpenLMStudio\) — clearing all logs and chats — then restarts the app.
    /// Preserves the entire models/ folder (except settings.json).
    /// </summary>
    /// <summary>
    /// Opens the About dialog.
    /// </summary>
    private void OnStatusAboutClicked(object? sender, RoutedEventArgs e)
    {
        var about = new AboutWindow();
        var parent = global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        about.ShowDialog(parent ?? this);
    }

    private void OnStatusResetClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Use the AppData directory (same location as ExampleChats)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDir = Path.Combine(appData, "OpenLMStudio");

            if (!Directory.Exists(settingsDir))
            {
                ShowError($"Settings directory not found:\n\n{settingsDir}");
                return;
            }

            // Ensure the models/safetensors subfolder exists
            var modelsDir = Path.Combine(settingsDir, "models");
            var safetensorsDir = Path.Combine(modelsDir, "safetensors");
            Directory.CreateDirectory(modelsDir);
            Directory.CreateDirectory(safetensorsDir);

            // Collect files to delete (everything inside AppData/OpenLMStudio, except models/ folder)
            var filesToDelete = new List<string>();

            foreach (var file in Directory.EnumerateFiles(settingsDir, "*", SearchOption.AllDirectories))
            {
                // Skip files in the models/ folder (use Path.Combine to handle both separators)
                var modelsDirWithSep = Path.Combine(modelsDir, "");
                if (file.StartsWith(modelsDirWithSep, StringComparison.OrdinalIgnoreCase) ||
                    file.StartsWith(modelsDir, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip settings.json (it will be preserved)
                var fileName = Path.GetFileName(file);
                if (fileName == "settings.json")
                    continue;

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

            // Delete empty directories (except models/)
            foreach (var dir in Directory.EnumerateDirectories(settingsDir, "*", SearchOption.AllDirectories).Reverse())
            {
                // Skip the models/ folder itself
                if (dir == modelsDir)
                    continue;

                // Skip subdirectories of models/
                if (dir.StartsWith(modelsDir, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    try { Directory.Delete(dir); } catch { /* ignore */ }
                }
            }

            _logger?.LogInformation("Wiped settings directory (preserved models/): {Dir}", settingsDir);

            // Restart the app
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                CreateNoWindow = true
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
        if (System.Threading.Interlocked.CompareExchange(ref _titleEditSaving, 1, 0) != 0)
            return;

        System.Threading.Interlocked.Exchange(ref _titleEditSaving, 1);

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
                System.Threading.Interlocked.Exchange(ref _titleEditSaving, 0);
            });
        }
        else
        {
            System.Threading.Interlocked.Exchange(ref _titleEditSaving, 0);
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

    /// <summary>
    /// Wires up ToggleButton Click handlers for the left sidebar tabs.
    /// All tabs are ToggleButtons with Click events wired to OnLeftTabClick for mutual exclusivity.
    /// </summary>
    private void WireUpLeftTabClickHandlers()
    {
        // All ToggleButtons already have Click="OnLeftTabClick" in XAML.
        // This method is kept for any additional programmatic wiring if needed.
    }

    /// <summary>
    /// Wires up the Settings sub-tab TabItems (Server/Model/Agent/Plugin/Privacy).
    /// </summary>
    private void WireUpSettingsSubTabs()
    {
        var subTabs = new (TabItem button, string tabName)[]
        {
            (SettingsServerSubTab, "Server"),
            (SettingsModelSubTab, "Model"),
            (SettingsAgentSubTab, "Agent"),
            (SettingsPluginSubTab, "Plugin"),
            (SettingsPrivacySubTab, "DataPrivacy"),
        };

        foreach (var (button, tabName) in subTabs)
        {
            if (button != null)
            {
                // TabItem doesn't have Click event in Avalonia — use PointerPressed instead
                button.PointerPressed += OnSettingsSubTabPointerPressed;
            }
        }
    }

    /// <summary>
    /// Handles clicks on the left sidebar ToggleButton tabs.
    /// Ensures mutual exclusivity — only one tab can be active at a time.
    /// </summary>
    private void OnLeftTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clickedTab)
            return;

        var tabName = clickedTab.Tag as string;
        if (string.IsNullOrEmpty(tabName))
            return;

        // Uncheck all other tabs (mutual exclusivity)
        var allTabs = new ToggleButton[]
        {
            SettingsTab, ChatTab, ServerTab, ModelsTab,
            DevicesTab, ContextTab, PinguTab, ImageGenTab
        };

        foreach (var tab in allTabs)
        {
            if (tab != null && tab != clickedTab)
                tab.IsChecked = false;
        }

        // Check the clicked tab
        clickedTab.IsChecked = true;

        // Show the tab content
        ShowTab(tabName);

        // Update right sidebar for Context tab
        if (tabName == "Context")
            UpdateRightSidebarTab("Context");
    }

    /// <summary>
    /// Handles PointerPressed on the settings sub-tab TabItems.
    /// </summary>
    private void OnSettingsSubTabPointerPressed(object? sender, PointerEventArgs e)
    {
        // Only process left mouse clicks
        var tabItem = (TabItem)sender!;
        var point = e.GetCurrentPoint(tabItem);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var subTabName = tabItem.Tag as string;

        if (string.IsNullOrEmpty(subTabName))
        {
            subTabName = tabItem.Name;
        }

        // Hide all settings panels
        SettingsServerPanel?.SetValue(StackPanel.IsVisibleProperty, false);
        SettingsModelPanel?.SetValue(StackPanel.IsVisibleProperty, false);
        SettingsAgentPanel?.SetValue(StackPanel.IsVisibleProperty, false);
        SettingsPluginPanel?.SetValue(StackPanel.IsVisibleProperty, false);
        SettingsPrivacyPanel?.SetValue(StackPanel.IsVisibleProperty, false);

        // Show the selected panel
        switch (subTabName)
        {
            case "Server":
                SettingsServerPanel?.SetValue(StackPanel.IsVisibleProperty, true);
                break;
            case "Model":
                SettingsModelPanel?.SetValue(StackPanel.IsVisibleProperty, true);
                break;
            case "Agent":
                SettingsAgentPanel?.SetValue(StackPanel.IsVisibleProperty, true);
                break;
            case "Plugin":
                SettingsPluginPanel?.SetValue(StackPanel.IsVisibleProperty, true);
                break;
            case "DataPrivacy":
                SettingsPrivacyPanel?.SetValue(StackPanel.IsVisibleProperty, true);
                break;
            default:
                _logger?.LogWarning("Unknown settings sub-tab: {SubTabName}", subTabName);
                break;
        }
    }

    /// <summary>
    /// Right sidebar is now simplified - only "Active Tasks" header and Pingu home tile.
    /// No tab handling needed.
    /// </summary>
    private void WireUpRightSidebarTabs()
    {
        // No-op: right sidebar is now simplified to just "Active Tasks" header + Pingu home tile.
    }

    /// <summary>
    /// Right sidebar is now simplified - only "Active Tasks" header and Pingu home tile.
    /// No tab handling needed.
    /// </summary>
    private void UpdateRightSidebarTab(string tabName)
    {
        // No-op: right sidebar is now simplified to just "Active Tasks" header + Pingu home tile.
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
        // Ensure AgentToolExecutor is resolved
        if (_agentToolExecutor == null)
        {
            _agentToolExecutor = ResolveAgentToolExecutorFromAppServices();
        }

        if (_agentToolExecutor == null)
        {
            ShowError("AgentToolExecutor is not available. Please ensure the agent services are properly configured.");
            return;
        }

        // Always refresh the tool list when opening
        if (ToolListPanel != null)
        {
            PopulateToolList();
        }

        // Show the popup
        if (ToolCallPopup != null)
        {
            ToolCallPopup.PlacementTarget = CenterPaneGrid;
            ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, true);
            _logger?.LogInformation("ToolCallPopup opened — Children={Count}", ToolListPanel?.Children.Count);
        }
        else
        {
            _logger?.LogWarning("ToolCallPopup is null in OnToolsButtonClicked");
        }
    }

    /// <summary>
    /// Populates the ToolListPanel with buttons for each available tool.
    /// </summary>
    private void PopulateToolList()
    {
        if (ToolListPanel == null || _agentToolExecutor == null)
            return;

        var tools = _agentToolExecutor.ListAvailableTools();
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
                ShowToolCallForm(tool, _agentToolExecutor);
            };

            ToolListPanel.Children.Add(btn);
        }
    }

    /// <summary>
    /// Shows the ToolCallForm popup for the given tool.
    /// </summary>
    private void ShowToolCallForm(ToolDefinition tool, IAgentToolExecutor executor)
    {
        // Close the tool list popup
        if (ToolCallPopup != null)
            ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);

        // Dispose any existing ToolCallForm
        if (_activeToolCallForm != null)
        {
            _activeToolCallForm.Dispose();
            _activeToolCallForm = null;
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
    /// Closes the tool popup when clicking on the center pane (outside the popup).
    /// </summary>
    private void OnCenterPanePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ToolCallPopup != null && ToolCallPopup.IsOpen)
        {
            var pos = e.GetPosition(ToolCallPopup);
            // If the click is outside the popup content, close it
            if (pos.X < 0 || pos.X > 440 || pos.Y < 0 || pos.Y > 500)
                ToolCallPopup.SetValue(Avalonia.Controls.Primitives.Popup.IsOpenProperty, false);
        }
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

    /// <summary>
    /// Opens the app data folder (C:\Users\Yonneh\AppData\Roaming\OpenLMStudio) in the file explorer.
    /// </summary>
    private async void OnStatusFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                ShowError("Application data folder not found. Cannot open app folder.");
                return;
            }
            var openPath = Path.Combine(appData, "OpenLMStudio");
            var psi = new ProcessStartInfo
            {
                FileName = openPath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open app folder");
            ShowError($"Failed to open app folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads all example chats from the ExampleChats folder (in AppData) and switches to the chat view.
    /// </summary>
    private async void OnStatusNewChatClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_conversationManager == null)
            {
                _logger?.LogWarning("ConversationManager not available");
                return;
            }

            // Always use AppData for ExampleChats (C:\Users\Yonneh\AppData\Roaming\OpenLMStudio\ExampleChats)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var exampleChatsDir = Path.Combine(appData, "OpenLMStudio", "ExampleChats");

            // Create the folder if it doesn't exist
            Directory.CreateDirectory(exampleChatsDir);

            // ExampleChats is now a permanent AppData folder — no copy needed.
            // The portable exe always reads/writes data from AppData, never from the exe directory.

            if (!Directory.Exists(exampleChatsDir))
            {
                _logger?.LogWarning("ExampleChats directory not found at {Dir}", exampleChatsDir);
                ShowError($"Couldn't find example chats at:\n\n{exampleChatsDir}\n\nPlease copy the ExampleChats folder (with *.json files) into this location.");
                return;
            }

            var chatFiles = Directory.GetFiles(exampleChatsDir, "*.json");
            var loaded = 0;

            // Await each import sequentially to avoid race conditions where multiple files
            // deserialize and save concurrently, causing the second file's save to overwrite
            // the first file's persisted state before it's written to disk.
            foreach (var file in chatFiles)
            {
                try
                {
                    await _conversationManager.ImportChatAsync(file).ConfigureAwait(false);
                    loaded++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load example chat {File}", file);
                }
            }

            // Switch to chat tab and refresh the list (after all imports complete)
            ShowTab("Chat");
            RefreshChatListAsync();

            _logger?.LogInformation("Loaded {Count} example chats", loaded);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load example chats");
            ShowError($"Failed to load example chats: {ex.Message}");
        }
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
    /// Caches the cached tab toggle buttons to avoid repeated visual tree traversal.
    /// </summary>
    private static List<ToggleButton>? _cachedTabButtons;
    private static DateTime _lastCacheTime;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Cycles the active tab by the given direction (+1 for forward, -1 for backward).
    /// Uses cached toggle buttons to avoid repeated visual tree traversal.
    /// </summary>
    public static void CycleTab(int direction)
    {
        if (_mainWindow == null) return;

        // Invalidate cache if it's stale
        if (_cachedTabButtons == null || DateTime.UtcNow - _lastCacheTime > CacheTtl)
        {
            // Look for ToggleButton elements in the left sidebar StackPanel via visual tree
            var leftPanel = _mainWindow.GetVisualDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "LeftTabStripPanel");
            _cachedTabButtons = leftPanel?.Children.OfType<ToggleButton>().ToList();
            _lastCacheTime = DateTime.UtcNow;
        }

        // Find the currently checked tab
        var selected = _cachedTabButtons?.FirstOrDefault(t => t.IsChecked == true);
        if (selected == null) return;

        var items = _cachedTabButtons!;
        var index = items.IndexOf(selected);
        if (index < 0) return;

        var nextIndex = (index + direction + items.Count) % items.Count;
        // Uncheck all tabs first
        foreach (var tab in items)
        {
            tab.IsChecked = false;
        }
        // Check the next tab
        items[nextIndex].IsChecked = true;

        // Show the tab content
        var tabName = items[nextIndex].Tag as string;
        if (!string.IsNullOrEmpty(tabName))
        {
            _mainWindow.ShowTab(tabName);
        }
    }
}
