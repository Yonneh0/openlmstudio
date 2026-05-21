// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents
// Brought to you by Carls' Jr.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.Pingu;
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

    /// <summary>Pingu avatar control for the bottom-right corner of the main window.</summary>
    private PinguAvatar? _pinguAvatar;

    /// <summary>Flag indicating whether a streaming response is in progress.</summary>
    private bool _isStreaming = false;

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
        IWindowSettings? windowSettings = null)
    {
        InitializeComponent();
        _logger = logger;

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

        // Initialize Pingu avatar if IPinguStore is available
        InitializePingu();

        // Defer chat list loading until after window is shown to avoid freezing the UI
        this.Opened += OnMainWindowOpened;
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        // Unsubscribe to avoid re-running
        this.Opened -= OnMainWindowOpened;
        RefreshChatListAsync();
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

        if (RightServerStartStopButton != null)
            RightServerStartStopButton.Click += OnServerStartStopClicked;

        // Handle Enter key in input box for sending messages
        if (MessageInputBox != null)
            MessageInputBox.KeyDown += OnMessageInputKeyDown;

        // Context tab custom context injection button
        if (InjectCustomContextBtn != null)
            InjectCustomContextBtn.Click += OnInjectCustomContextClicked;

        if (RightAddCustomContextBtn != null)
            RightAddCustomContextBtn.Click += OnRightAddCustomContextClicked;

        if (RightCustomContextInjectBtn != null)
            RightCustomContextInjectBtn.Click += OnRightCustomContextInjectClicked;

        // Context compression selector (left sidebar)
        if (ContextCompressionSelector != null)
            ContextCompressionSelector.SelectionChanged += OnContextCompressionSelectionChanged;

        // Context compression selector (right sidebar)
        if (RightCompressionSelector != null)
            RightCompressionSelector.SelectionChanged += OnRightCompressionSelectionChanged;

        // Random seed button
        if (RandomSeedButton != null)
            RandomSeedButton.Click += OnRandomSeedClicked;

        // Image generation generate button
        if (ImageGenGenerateBtn != null)
            ImageGenGenerateBtn.Click += OnImageGenGenerateClicked;

        // Image generation model selector
        if (ImageGenModelSelector != null)
            ImageGenModelSelector.SelectionChanged += OnImageGenModelSelectorSelectionChanged;

        // Settings button
        if (SettingsButton != null)
            SettingsButton.Click += OnSettingsClicked;

        // Refresh devices button
        if (RightRefreshDevicesBtn != null)
            RightRefreshDevicesBtn.Click += OnRefreshDevicesClicked;

        // Menu bar buttons
        if (MenuOpenModel != null)
            MenuOpenModel.Click += OnOpenModelClicked;

        if (MenuExit != null)
            MenuExit.Click += OnExitClicked;

        if (MenuMinesweeper != null)
            MenuMinesweeper.Click += OnOpenMinesweeperClicked;

        if (MenuTetris != null)
            MenuTetris.Click += OnOpenTetrisClicked;

        if (MenuSnake != null)
            MenuSnake.Click += OnOpenSnakeClicked;

        if (MenuJezzball != null)
            MenuJezzball.Click += OnOpenJezzballClicked;

        if (MenuSolitaire != null)
            MenuSolitaire.Click += OnOpenSolitaireClicked;
    }
}