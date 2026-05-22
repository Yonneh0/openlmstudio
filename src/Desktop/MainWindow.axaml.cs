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
    private readonly IAgent? _agentService;

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
        IAgent? agentService = null,
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
        _agentService = agentService;
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

        // Agent tab handlers
        if (AgentStartButton != null)
            AgentStartButton.Click += OnAgentStartClicked;

        if (AgentStopButton != null)
            AgentStopButton.Click += OnAgentStopClicked;

        if (AgentMaxIterationsSlider != null)
            AgentMaxIterationsSlider.ValueChanged += OnAgentMaxIterationsValueChanged;

        // Task tab handlers
        if (CreateTaskButton != null)
            CreateTaskButton.Click += OnCreateTaskClicked;

        if (TasksTabTitle != null)
            TasksTabTitle.PointerPressed += OnTasksTabPointerPressed;

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

        // Git status bar
        if (GitStatusBorder != null)
            GitStatusBorder.PointerPressed += OnGitStatusClicked;

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

    // =========================================================================
    // Agent tab handlers
    // =========================================================================

    private void OnAgentTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ShowTab("Agent");
    }

    private void OnAgentStartClicked(object? sender, RoutedEventArgs e)
    {
        var taskDescription = AgentTaskInput?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            _logger?.LogWarning("Agent task description is empty");
            return;
        }

        var maxIterations = (int)(AgentMaxIterationsSlider?.Value ?? 50);
        _logger?.LogInformation("Starting agent task: {Task}, max iterations: {Max}", taskDescription, maxIterations);

        AgentStartButton?.SetValue(Button.IsEnabledProperty, false);
        AgentStopButton?.SetValue(Button.IsVisibleProperty, true);

        if (_agentService != null)
        {
            try
            {
                var taskRequest = new AgentTaskRequest(
                    Guid.NewGuid(),
                    taskDescription,
                    MaxIterations: maxIterations);

                _ = Task.Run(async () => await _agentService.ExecuteAsync(taskRequest, CancellationToken.None));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start agent task");
                AgentStartButton?.SetValue(Button.IsEnabledProperty, true);
                AgentStopButton?.SetValue(Button.IsVisibleProperty, false);
            }
        }
        else
        {
            _logger?.LogWarning("IAgent service not available");
            AgentStartButton?.SetValue(Button.IsEnabledProperty, true);
            AgentStopButton?.SetValue(Button.IsVisibleProperty, false);
        }
    }

    private void OnAgentStopClicked(object? sender, RoutedEventArgs e)
    {
        _logger?.LogInformation("Stopping agent");
        if (_agentService != null)
        {
            try
            {
                _ = _agentService.AbortAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to abort agent");
            }
        }
        AgentStartButton?.SetValue(Button.IsEnabledProperty, true);
        AgentStopButton?.SetValue(Button.IsVisibleProperty, false);
    }

    private void OnAgentMaxIterationsValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (AgentMaxIterationsText != null)
            AgentMaxIterationsText.Text = ((int)(AgentMaxIterationsSlider?.Value ?? 50)).ToString();
    }

    // =========================================================================
    // Task tab handlers
    // =========================================================================

    private void OnTasksTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ShowTab("Tasks");
    }

    private void OnCreateTaskClicked(object? sender, RoutedEventArgs e)
    {
        var description = NewTaskDescriptionInput?.Text;
        if (string.IsNullOrWhiteSpace(description))
        {
            _logger?.LogWarning("Task description is empty");
            return;
        }

        var priorityText = TaskPrioritySelector?.SelectedItem as TextBlock;
        var priority = priorityText?.Text switch
        {
            "Low" => TaskPriority.Low,
            "High" => TaskPriority.High,
            "Critical" => TaskPriority.Critical,
            _ => TaskPriority.Normal
        };

        _logger?.LogInformation("Creating task: {Description}, priority: {Priority}", description, priority);

        // Use TaskService to create the task
        var sp = GetAppServiceProvider();
        var taskService = sp?.GetService<OpenLMStudio.Application.Interfaces.ITaskService>();
        if (taskService != null)
        {
            _ = Task.Run(async () =>
            {
                var task = await taskService.CreateTaskAsync(description, priority: priority);
                // Refresh task list on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => RefreshTaskListAsync());
            });
        }
    }

    private void RefreshTaskListAsync()
    {
        var sp = GetAppServiceProvider();
        var taskService = sp?.GetService<OpenLMStudio.Application.Interfaces.ITaskService>();
        if (taskService == null || TaskListPanel == null)
            return;

        TaskListPanel.Children.Clear();

        var tasks = taskService.GetTasks();
        if (!tasks.Any())
        {
            TaskListPanel.Children.Add(new TextBlock
            {
                Text = "No tasks created",
                Foreground = (SolidColorBrush)(this.FindResource("TextMuted") ?? Avalonia.Media.Brushes.Gray),
                Padding = new Thickness(12, 8),
                FontSize = 12
            });
            return;
        }

        foreach (var task in tasks)
        {
            var statusColor = task.Status switch
            {
                Domain.Models.TaskStatus.Completed => "#4CAF50",
                Domain.Models.TaskStatus.Running => "#FF9800",
                Domain.Models.TaskStatus.Failed => "#FF6B6B",
                Domain.Models.TaskStatus.Cancelled => "#888888",
                _ => "#888888"
            };

            var statusText = task.Summary ?? task.Status.ToString();

            var stack = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 6)
            };

            stack.Children.Add(new TextBlock
            {
                Text = task.Description,
                Foreground = (SolidColorBrush)(this.FindResource("TextPrimary") ?? Avalonia.Media.Brushes.White),
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"[{task.Priority}] {statusText}",
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(statusColor)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            });

            TaskListPanel.Children.Add(stack);
        }
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
