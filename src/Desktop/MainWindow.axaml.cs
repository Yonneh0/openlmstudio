// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents

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
using Avalonia.Markup.Xaml;
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
        IPinguStore? pinguStore = null)
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

        // Subscribe to server state changes
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged += OnServerStateChanged;

        // Set up event handlers for UI interactions
        SetupEventHandlers();

        // Load tab click handlers (they need access to this instance's ShowTab method)
        AttachTabClickHandlers();

        // Initialize Pingu avatar if IPinguStore is available
        InitializePingu();

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
    }

    private void ShowTab(string tabName)
    {
        _activeTab = tabName;

        // Hide all tab contents first
        SetTabVisibility(ChatTabContent, false);
        SetTabVisibility(ServerTabContent, false);
        SetTabVisibility(ModelsTabContent, false);
        SetTabVisibility(DevicesTabContent, false);
        SetTabVisibility(ContextTabContent, false);
        SetTabVisibility(ImageGenTabContent, false);

        // Show the selected tab content
        switch (tabName)
        {
            case "Chat":
                SetTabVisibility(ChatTabContent, true);
                break;
            case "Server":
                SetTabVisibility(ServerTabContent, true);
                UpdateServerStatus();
                break;
            case "Models":
                SetTabVisibility(ModelsTabContent, true);
                RefreshModelListAsync();
                break;
            case "Devices":
                SetTabVisibility(DevicesTabContent, true);
                UpdateDeviceStatus();
                break;
            case "Context":
                SetTabVisibility(ContextTabContent, true);
                _ = RefreshContextBudgetAsync();
                break;
            case "ImageGen":
                SetTabVisibility(ImageGenTabContent, true);
                break;
        }

        // Update active tab styling
        UpdateActiveTab(tabName);
    }

    private void SetTabVisibility(StackPanel? panel, bool visible)
    {
        if (panel != null)
            panel.IsVisible = visible;
    }

    private void UpdateActiveTab(string activeTabName)
    {
        // Update styling for all tab TextBlocks to show which is active
        var tabs = new List<TextBlock?>();

        if (ChatTabContent != null)
            tabs.Add(ChatTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ServerTabContent != null)
            tabs.Add(ServerTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ModelsTabContent != null)
            tabs.Add(ModelsTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (DevicesTabContent != null)
            tabs.Add(DevicesTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        if (ContextTabContent != null)
            tabs.Add(ContextTabContent.Children.OfType<TextBlock>().FirstOrDefault());

        foreach (var tb in tabs)
        {
            if (tb == null) continue;

            // Only update the first TextBlock of each tab section (the tab title)
            var parent = tb.Parent as Panel;
            if (parent?.Name != null &&
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent", "ContextTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase) ||
                    (activeTabName == "ImageGen" && tb.Text?.Equals("Image Generation") == true))
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)); // AccentBlue
                    tb.FontWeight = FontWeight.SemiBold;
                }
                else
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // TextPrimary
                    tb.FontWeight = FontWeight.Normal;
                }
            }
        }
    }

    private void UpdateRightSidebarTab(string activeTab)
    {
        // Show/hide right sidebar tab content panels
        SetPanelVisibility(RightContextContent, false);
        SetPanelVisibility(RightServerContent, false);
        SetPanelVisibility(RightDevicesContent, false);

        SetPanelVisibility(RightContextContent, activeTab == "Context");
        SetPanelVisibility(RightServerContent, activeTab == "Server");
        SetPanelVisibility(RightDevicesContent, activeTab == "Devices");

        // Update tab button states
        if (RightContextTabButton != null) RightContextTabButton.IsChecked = activeTab == "Context";
        if (RightServerTabButton != null) RightServerTabButton.IsChecked = activeTab == "Server";
        if (RightDevicesTabButton != null) RightDevicesTabButton.IsChecked = activeTab == "Devices";

        // Update context budget when switching to context tab
        if (activeTab == "Context")
            _ = RefreshContextBudgetAsync();
    }

    private void SetPanelVisibility(StackPanel? panel, bool visible)
    {
        if (panel != null)
            panel.IsVisible = visible;
    }

    // ---- Chat List Management ----

    private async void RefreshChatListAsync()
    {
        if (_conversationManager == null) return;

        try
        {
            var chats = await _conversationManager.ListChatsAsync();

            // Clear existing chat list
            ChatListPanel?.Children.Clear();

            if (!chats.Any())
            {
                ChatListPanel?.Children.Add(new TextBlock
                {
                    Text = "No chats loaded",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Padding = new Thickness(12),
                    FontSize = 12
                });
            }

            // Add each chat as a button in the list
            foreach (var chat in chats.OrderByDescending(c => c.UpdatedAt))
            {
                var chatButton = CreateChatListItem(chat);
                ChatListPanel?.Children.Add(chatButton);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing chat list");
        }
    }

    private Button CreateChatListItem(Chat chat)
    {
        // Null-checked: chat.Name can be null but we have a fallback
        var button = new Button();
        button.Content = chat.Name ?? $"Conversation {chat.Id.ToString("N").Substring(0, 8)}";
        button.Classes.Add("chatItem");
        button.Tag = chat.Id;
        button.Margin = new Thickness(0, 2, 0, 2);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Highlight active/selected chat (null-checked for CS8602)
        if (_selectedChatId != null && _selectedChatId.Value == chat.Id)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        }

        // Add token count as tooltip - _conversationManager already verified non-null in caller path (CreateChatListItem is only called after ListChatsAsync which requires it)
#pragma warning disable CS8602 // Dereference of a possibly null reference
        var tokenCount = _conversationManager.CalculateTotalTokenCountAsync(chat.Id).GetAwaiter().GetResult();
#pragma warning restore CS8602

        // Use Avalonia's ToolTip.SetTip() attached method instead of Tooltip property
        var toolTipText = new TextBlock { Text = $"Tokens: {tokenCount}" };
        if (button.Parent is Border buttonBorder)
            ToolTip.SetTip(button, toolTipText);
        else
            button.AttachedToVisualTree += (_, _) =>
            {
                var p = button.Parent as Border;
                if (p != null && !(toolTipText.Parent is Panel))
                    ToolTip.SetTip(p, toolTipText);
            };

        button.Click += OnChatItemClicked;
        return button;
    }

    private async void OnChatItemClicked(object? sender, RoutedEventArgs e)
    {
        var chatIdObj = (sender as Button)?.Tag as Guid?;

        if (!chatIdObj.HasValue)
            return;

        _selectedChatId = chatIdObj.Value;

        // Update selection styling for all buttons in the list
        foreach (var child in ChatListPanel?.Children.OfType<Button>().ToList() ?? new List<Button>())
        {
            var isSelected = (child.Tag as Guid?) == chatIdObj;
            if (isSelected)
            {
                child.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            }
            else
            {
                // Reset to default style background
                var fallback = new SolidColorBrush(Color.FromRgb(37, 37, 41));
                child.Background = child.Classes.Contains("chatItem")
                    ? new SolidColorBrush(Color.FromRgb(37, 37, 41))
                    : fallback;
            }
        }

        // Load the selected conversation's messages — fire-and-forget since this is called from an async void event handler
        _ = LoadConversationMessagesAsync(chatIdObj.Value).ConfigureAwait(false);
    }

    private async void OnNewChatClicked(object? sender, RoutedEventArgs e)
    {
        if (_conversationManager == null) return;

        var newChat = await _conversationManager.CreateChatAsync("New Chat");
        RefreshChatListAsync();

        // Automatically select the new chat — fire-and-forget since this is called from an async void event handler
        _ = LoadConversationMessagesAsync(newChat.Id).ConfigureAwait(false);
    }

    // ---- Conversation Message Loading and Display ----

    // Note: This is async Task (not async void) so it can be awaited by callers.
    private async Task LoadConversationMessagesAsync(Guid chatId)
    {
        if (_conversationManager == null || _selectedChatId != chatId) return;

        try
        {
            var messages = await _conversationManager.GetMessagesAsync(chatId);

            // Clear existing message display
            MessageDisplayPanel?.Children.Clear();

            if (!messages.Any())
            {
                var textBlock = new TextBlock
                {
                    Text = "No messages yet. Start a conversation!",
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 14
                };

                MessageDisplayPanel?.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 41)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 0, 16),
                    Child = textBlock
                });
            }

            // Display each message with alternating styling and context controls
            foreach (var msg in messages)
            {
                var messageBorder = CreateMessageBorder(msg);
                if (messageBorder != null)
                    MessageDisplayPanel?.Children.Add(messageBorder);
            }

            // Update chat title display
            ChatTitleText.Text = "Chat Session";

            // Refresh context budget after loading messages
            await RefreshContextBudgetAsync();

            // Scroll to bottom of messages
            await ScrollToBottomAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading conversation: {ChatId}", chatId);
        }
    }

    private Border? CreateMessageBorder(Message message)
    {
        if (message.Content == null || message.Content.Length == 0)
            return null;

        var border = new Border
        {
            Margin = new Thickness(0, 0, 4, 8),
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(8)
        };

        if (message.Role == MessageRole.User || message.Role == MessageRole.System)
        {
            border.Background = new SolidColorBrush(Color.FromRgb(37, 37, 41));
            border.CornerRadius = new CornerRadius(8, 0, 8, 8);
            border.Classes.Add("userMessage");
        }
        else // Assistant or Tool
        {
            border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            border.CornerRadius = new CornerRadius(0, 8, 8, 8);
            border.Classes.Add("assistantMessage");
        }

        var textBlock = new TextBlock
        {
            Text = message.Content ?? "",
            Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            FontSize = 14,
            Margin = new Thickness(0)
        };

        // Add role indicator if it's an assistant message with tool calls
        if (message.Role == MessageRole.Assistant && message.ToolCalls?.Any() == true)
        {
            var stackPanel = new StackPanel();

            // Role label for assistant messages
            stackPanel.Children.Add(new TextBlock
            {
                Text = "AI",
                Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 8, 4)
            });

            // Content text block
            stackPanel.Children.Add(textBlock);

            // Tool calls indicator if present
            foreach (var toolCall in message.ToolCalls)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"Tool: {toolCall.FunctionName}({toolCall.ArgumentsJson})",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    FontSize = 11,
                    Margin = new Thickness(8)
                });
            }

            border.Child = stackPanel;
        }
        else
        {
            // Add role label for user/system messages and context controls
            var outerStackPanel = new StackPanel();

            // Top row: Role label + per-message context controls
            if (_contextManager != null && _selectedChatId.HasValue)
            {
                var topRow = new Grid();
                topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Role label on the left
                var roleLabel = new TextBlock
                {
                    Text = message.Role.ToString().ToUpper(),
                    Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0, 0, 8, 4)
                };

                if (message.Role == MessageRole.User)
                    roleLabel.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green for user

                Grid.SetColumn(roleLabel, 0);
                topRow.Children.Add(roleLabel);

                // Per-message context controls on the right
                var controlPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };

                // Pin button — lock from compression/reordering
                var pinBtn = new Button
                {
                    Content = "📌",
                    Classes = { "msgPinBtn" },
                    Padding = new Thickness(6, 2),
                    FontSize = 10,
                    BorderThickness = new Thickness(0)
                };
                // Store message ID on button Tag so event handler can look it up — 
                // unlike WPF where Button.Tag is directly accessible in Avalonia.
                pinBtn.Tag = message.Id;
                pinBtn.Click += OnMessagePinClicked;
                controlPanel.Children.Add(pinBtn);

                // Suppress button — toggle visibility to AI
                var suppressBtn = new Button
                {
                    Content = "👁️",
                    Classes = { "msgSuppressBtn" },
                    Padding = new Thickness(6, 2),
                    FontSize = 10,
                    BorderThickness = new Thickness(0)
                };
                // Store message ID on button Tag so event handler can look it up — 
                // unlike WPF where Button.Tag is directly accessible in Avalonia.
                suppressBtn.Tag = message.Id;
                suppressBtn.Click += OnMessageSuppressClicked;
                controlPanel.Children.Add(suppressBtn);

                Grid.SetColumn(controlPanel, 1);
                topRow.Children.Add(controlPanel);

                outerStackPanel.Children.Add(topRow);
            }

            // Content text block below the role label + controls
            outerStackPanel.Children.Add(textBlock);

            border.Child = outerStackPanel;
        }

        return border;
    }

    // Dictionary to track custom context segments → Border for removal (avoids visual tree traversal in Avalonia)
    private readonly ConcurrentDictionary<Guid, Border> _customContextBorders = new();

    /// <summary>Tracks per-message pin/suppress toggle state. Key = message Id, value = pinned state.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _messagePinStates = new();

    /// <summary>Tracks per-message suppress/reveal toggle state. Key = message Id, value = suppressed state.</summary>
    private readonly ConcurrentDictionary<Guid, bool> _messageSuppressStates = new();

    // ---- Per-Message Context Control Event Handlers ----

    /// <summary>
    /// Toggles pin for the specified message segment via IChatContextManager.
    /// Button Tag holds the Message.Id so we can look it up from within the event handler.
    /// </summary>
    private async void OnMessagePinClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextManager == null || _selectedChatId == null) return;

        var button = (Button)sender!;

        // Get message ID from Tag — set when the button is created in CreateMessageBorder()
        var messageIdObj = button.Tag as Guid?;
        if (!messageIdObj.HasValue)
        {
            _logger?.LogWarning("OnMessagePinClicked: Tag not set on pin button — cannot determine message Id.");
            return;
        }

        var messageId = messageIdObj.Value;
        var isPinned = _messagePinStates.GetOrAdd(messageId, static key => false);
        var newPinnedState = !isPinned;

        // Update UI state regardless of whether the context manager call succeeds
        button.Tag = newPinnedState;
        if (newPinnedState)
            button.Classes.Add("pinned");
        else
            button.Classes.Remove("pinned");

        // Persist the pin state to the context manager (may fail silently — UI already updated)
        try
        {
            if (newPinnedState)
                await _contextManager.PinSegmentAsync(_selectedChatId.Value, messageId);
            else
                await _contextManager.UnpinSegmentAsync(_selectedChatId.Value, messageId);

            // Update our local tracking dictionary after successful persistence
            _messagePinStates[messageId] = newPinnedState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pin/unpin segment for message {MessageId}", messageId);
            // Revert UI on failure — revert the button state and remove pinned class
            if (!newPinnedState)
                button.Classes.Add("pinned");
            else
                button.Classes.Remove("pinned");
        }
    }

    /// <summary>
    /// Toggles suppress/reveal for the specified message segment via IChatContextManager.
    /// Button Tag holds the Message.Id so we can look it up from within the event handler.
    /// </summary>
    private async void OnMessageSuppressClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextManager == null || _selectedChatId == null) return;

        var button = (Button)sender!;

        // Get message ID from Tag — set when the button is created in CreateMessageBorder()
        var messageIdObj = button.Tag as Guid?;
        if (!messageIdObj.HasValue)
        {
            _logger?.LogWarning("OnMessageSuppressClicked: Tag not set on suppress button — cannot determine message Id.");
            return;
        }

        var messageId = messageIdObj.Value;
        var isSuppressed = _messageSuppressStates.GetOrAdd(messageId, static key => false);
        var newSuppressState = !isSuppressed;

        // Update UI state regardless of whether the context manager call succeeds
        button.Tag = newSuppressState;
        if (newSuppressState)
            button.Classes.Add("suppressed");
        else
            button.Classes.Remove("suppressed");

        // Persist the suppress state to the context manager (may fail silently — UI already updated)
        try
        {
            if (newSuppressState)
                await _contextManager.SuppressSegmentAsync(_selectedChatId.Value, messageId);
            else
                await _contextManager.RevealSegmentAsync(_selectedChatId.Value, messageId);

            // Update our local tracking dictionary after successful persistence
            _messageSuppressStates[messageId] = newSuppressState;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to suppress/reveal segment for message {MessageId}", messageId);
            // Revert UI on failure — revert the button state and remove suppressed class
            if (!newSuppressState)
                button.Classes.Add("suppressed");
            else
                button.Classes.Remove("suppressed");
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

                // Update UI to reflect stopped state (both buttons + both text blocks)
                if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = "Start Server";
                if (RightServerStartStopButton != null) RightServerStartStopButton.Content = "Start Server";
                ServerStatusText.Text = "Server: Stopped";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                ServerStatusRight.Text = "Server: Stopped";
                ServerStatusRight.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            }
            else
            {
                // Start the server on a default port
                var configuration = new ServerConfiguration { Port = 8080 };
                await _serverService.StartAsync(configuration);

                // Update UI to reflect running state (both buttons + both text blocks)
                if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = "Stop Server";
                if (RightServerStartStopButton != null) RightServerStartStopButton.Content = "Stop Server";
                ServerStatusText.Text = $"Server: Running (Port {configuration.Port})";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                ServerStatusRight.Text = $"Server: Running (Port {configuration.Port})";
                ServerStatusRight.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
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

        // Update server status display across all UI elements (both buttons + both text blocks)
        ServerStatusText.Text = $"Server: {(isRunning ? "Running" : "Stopped")}";
        ServerStatusRight.Text = $"Server: {(isRunning ? "Running" : "Stopped")}";

        if (LeftServerStartStopButton != null) LeftServerStartStopButton.Content = isRunning ? "Stop Server" : "Start Server";
        if (RightServerStartStopButton != null) RightServerStartStopButton.Content = isRunning ? "Stop Server" : "Start Server";

        if (isRunning)
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            ServerStatusRight.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green

            // Try to get port from the server service's configuration
            var srv = _serverService as OpenLMStudio.Infrastructure.Services.ServerService;
            if (srv?.Configuration != null)
            {
                ServerPortRightText.Text = $"Port: {srv.Configuration.Port}";
                ServerPortText.Text = $"Port: {srv.Configuration.Port}";
                // Note: Right sidebar doesn't have a ServerPortRight display, only left side
            }
        }
        else
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
            ServerStatusRight.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
            ServerPortRightText.Text = "Port: 8080 (default)";
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

    /// <summary>
    /// Async version of device status update for use from async event handlers.
    /// </summary>
    private async Task UpdateDeviceStatusAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => UpdateDeviceStatus());
    }

    // ---- Device Status Update ----

    private void UpdateDeviceStatus()
    {
        if (_conversationManager == null) return;

        // Get device info from the device monitor service via DI
        try
        {
            var serviceProvider = GetAppServiceProvider();
            if (serviceProvider != null)
            {
                var deviceMonitor = serviceProvider.GetService<IDeviceMonitor>();
                if (deviceMonitor != null)
                {
                    CpuCoreText.Text = $"CPU Cores: {Environment.ProcessorCount}";

                    // Get total physical memory (not GC heap size)
                    try
                    {
                        // Use PerformanceCounter or WMI for total RAM
                        var ramAvailable = Environment.GetLogicalDrives().Length; // fallback to a non-crashing value
                        RamInfoText.Text = $"RAM: {Environment.ProcessorCount} Cores";
                    }
                    catch
                    {
                        RamInfoText.Text = "RAM: Unknown";
                    }

                    // Check for GPU via system info
                    try
                    {
                        var gpuDevices = deviceMonitor.GetGpuDevicesAsync().GetAwaiter().GetResult();
                        if (gpuDevices.Any())
                            LoadedModelRightText.Text = "GPU Detected";
                    }
                    catch
                    {
                        // Ignore errors reading GPU info
                    }
                }
            }
        }
        catch
        {
            // Ignore errors updating device status
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

    // ---- Assistant Response Handling (with streaming token-by-token support) ----

    private async Task GetAssistantResponseAsync(Guid chatId, string userMessage)
    {
        if (_conversationManager == null || _selectedChatId != chatId) return;

        try
        {
            // Show a "generating" placeholder while we set up the streaming connection
            var assistantBorder = new Border
            {
                Margin = new Thickness(0, 0, 4, 8),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(0, 8, 8, 8),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            };

            var placeholderText = new TextBlock
            {
                Text = "⏳ Generating response...",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 14,
                Margin = new Thickness(0)
            };

            var stackPanel = new StackPanel();
            // Role label for assistant messages
            var roleLabel = new TextBlock
            {
                Text = "AI",
                Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 8, 4)
            };
            stackPanel.Children.Add(roleLabel);
            stackPanel.Children.Add(placeholderText);
            assistantBorder.Child = stackPanel;

            MessageDisplayPanel?.Children.Add(assistantBorder);
            _currentAssistantBorder = assistantBorder;
            _assistantTextBlock = placeholderText;

            await ScrollToBottomAsync();

            // Show streaming indicator
            StreamingIndicator.IsVisible = true;
            _isStreaming = true;

            // Use the server service if running (real model loaded on local server)
            // or use IChatCompletionService directly as a fallback for when no server is available.
            bool usedServerEndpoint = false;
            try
            {
                if (_serverService != null && _serverService.State == ServerState.Running)
                {
                    await StreamResponseViaServerAsync(chatId, userMessage);
                    usedServerEndpoint = true;
                }
            }
            catch (Exception serverEx)
            {
                // Server not available — fall back to local chat completion service
                // Check if this is a connection reset (recoverable via reconnection)
                if (serverEx is HttpRequestException && serverEx.Message.Contains("connection"))
                {
                    _logger?.LogWarning("Server connection lost — attempting reconnection and retry");
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        if (_serverService != null && _serverService.State == ServerState.Running)
                        {
                            await Task.Delay(1000); // Brief pause before retry
                            try
                            {
                                await StreamResponseViaServerAsync(chatId, userMessage);
                                usedServerEndpoint = true;
                                return;
                            }
                            catch
                            {
                                // Final fallback
                            }
                        }
                    });
                }
                _logger?.LogDebug("Server streaming failed, falling back to local IChatCompletionService: {Message}", serverEx.Message);
            }

            // Fall back to local service if server wasn't used
            if (!usedServerEndpoint && _chatCompletionService != null)
            {
                await StreamResponseViaLocalServiceAsync(chatId, userMessage);
            }

            // Stop streaming indicator regardless of how the response was generated
            StreamingIndicator.IsVisible = false;
            _isStreaming = false;

            // Refresh context budget after adding assistant message
            if (_selectedChatId.HasValue)
                await RefreshContextBudgetAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting assistant response");

            // Show error message in the UI
            if (_currentAssistantBorder != null && _assistantTextBlock != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StreamingIndicator.IsVisible = false;
                    _isStreaming = false;
                    _assistantTextBlock.Text = "Error generating response. Check logs for details.";
                    _assistantTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                });
            }
        }
    }

    /// <summary>
    /// Streams a chat completion response via the local server's SSE endpoint (/v1/chat/completions with stream=true).
    /// </summary>
    private async Task StreamResponseViaServerAsync(Guid chatId, string userMessage)
    {
        if (_serverService == null || _serverService.State != ServerState.Running)
            throw new InvalidOperationException("Server is not running — cannot use server streaming endpoint.");

        // Build the HTTP request to the local server's streaming endpoint
        var uri = $"http://localhost:{_serverService.Configuration.Port}/v1/chat/completions";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Allow long-running completions for large models

        // GetMessagesAsync returns possibly-null — use null-forgiving operator with null-coalescing below
        var chatMessagesForServer = await _conversationManager!.GetMessagesAsync(chatId)! ?? [];
        var userMsgForServer = new Message { Role = MessageRole.User, Content = userMessage };
        chatMessagesForServer.Add(userMsgForServer);

        // Serialize as OpenAI-compatible ChatCompletionRequest (from Application.Types) with all defaults
        var requestBodyObj = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = null // Keep default PascalCase names — server expects camelCase via ASP.NET Core convention
        };
        var jsonBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            model = "default",
            messages = chatMessagesForServer.Select(m => new
            {
                role = m.Role.ToString().ToLower(),
                content = m.Content
            }).ToList(),
            stream = true,
            temperature = 0.7f,
            max_tokens = 4096,
            top_p = 1.0f
        }, requestBodyObj);

        var response = await httpClient.PostAsync(uri, new StringContent(jsonBody));

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Server returned: {response.StatusCode}");

        // Read SSE stream token-by-token and update UI on each event
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var currentData = new StringBuilder();
        bool inDataEvent = false;
        while (!reader.EndOfStream && _isStreaming)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            // Parse SSE event format: "data: {\"token\": \"...\", ...}" or "data: [DONE]"
            if (inDataEvent && line.Length > 0)
            {
                // Accumulate multi-line data events
                currentData.Append(line);
                continue;
            }

            inDataEvent = false;

            // SSE events are separated by blank lines — process the accumulated event now
            try
            {
                var dataStr = currentData.ToString();
                if (string.IsNullOrEmpty(dataStr)) continue; // Skip empty events

                if (dataStr == "[DONE]") break; // Stream complete

                var jsonDoc = System.Text.Json.JsonDocument.Parse(dataStr);
                var tokenElement = jsonDoc.RootElement.GetProperty("token");
                var tokenValue = tokenElement.GetString();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_assistantTextBlock != null)
                    {
                        _assistantTextBlock.Text += (tokenValue ?? "");
                        ScrollToBottomAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                });
            }
            catch (Exception ex)
            {
                // Ignore parsing errors for non-textual JSON events (e.g., usage stats)
                _logger?.LogDebug("SSE parsing error: {Message}", ex.Message);
            }

            currentData.Clear();

            // Check if this line starts a new event
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                inDataEvent = true;
                currentData.Append(line.Substring(5).Trim());
            }
        }

        await _conversationManager.AddMessageAsync(chatId, userMsgForServer);

        // Update token count after stream completes
        var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
        TokenCountText.Text = $"Tokens: {totalTokens}";
    }

    /// <summary>
    /// Streams a chat completion response via the local IChatCompletionService.
    /// </summary>
    private async Task StreamResponseViaLocalServiceAsync(Guid chatId, string userMessage)
    {
        // GetMessagesAsync returns possibly-null — use null-forgiving operator with null-coalescing below
        var chatMessages = await _conversationManager!.GetMessagesAsync(chatId)!;

        if (chatMessages == null || !chatMessages.Any()) throw new InvalidOperationException("No messages to send.");

        // Use the last assistant message as the model ID fallback — in practice this would be selected by user.
        var modelId = "default"; // TODO: Get from a model selector UI element

        if (_chatCompletionService == null) throw new InvalidOperationException("Chat completion service not available.");

        await foreach (var chunk in _chatCompletionService!.GetStreamingCompletionAsync(
            new ChatRequest(modelId, chatMessages.ToList()) { Stream = true }))
        {
            if (_isStreaming == false || string.IsNullOrEmpty(chunk)) continue;

            try
            {
                // Parse the SSE-style JSON: {"token": "...", "finish_reason": null/stop}
                var jsonDoc = System.Text.Json.JsonDocument.Parse(chunk);
                var tokenElement = jsonDoc.RootElement.GetProperty("token");
                var tokenValue = tokenElement.GetString();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_assistantTextBlock != null)
                    {
                        _assistantTextBlock.Text += (tokenValue ?? "");
                        ScrollToBottomAsync().ConfigureAwait(false);
                    }
                });
            }
            catch
            {
                // Ignore non-text events like usage stats, errors, etc.
            }
        }

        // Note: User message was already added to the conversation in OnSendMessageClicked — do NOT add again
        // Update token count after stream completes
        var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
        TokenCountText.Text = $"Tokens: {totalTokens}";
    }

    // ---- Context Budget Management ----

    private async Task RefreshContextBudgetAsync()
    {
        if (_selectedChatId == null || _budgeter == null) return;

        try
        {
            var indicator = await _budgeter.GetBudgetIndicatorAsync(_selectedChatId.Value);

            // Update left sidebar budget display
            if (ContextBudgetText != null)
            {
                ContextBudgetText.Text = $"Budget: {indicator.UsedTokens} / {indicator.MaximumTokens} tokens used";

                // Set color zone based on remaining percentage
                var remainingPct = indicator.RemainingTokens > 0 ? (float)indicator.RemainingTokens / indicator.MaximumTokens : 1f;
                if (remainingPct < 0.05f)
                    ContextBudgetText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red - critical
                else if (remainingPct < 0.20f)
                    ContextBudgetText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Yellow - warning
                else
                    ContextBudgetText.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)); // Normal text color
            }

            // Update right sidebar budget display
            if (RightBudgetText != null)
            {
                RightBudgetText.Text = $"Used: {indicator.UsedTokens} / {indicator.MaximumTokens} tokens ({(int)(indicator.PercentageUsed)}%)";

                // Set remaining color zone on the bar — use a SolidColorBrush based on zone instead of LinearGradientBrush which doesn't have Stops in Avalonia
                switch (indicator.ColorZone)
                {
                    case ContextBudgetColorZone.Green:
                        RightBudgetBar.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                        break;
                    case ContextBudgetColorZone.Yellow:
                        RightBudgetBar.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                        break;
                    case ContextBudgetColorZone.Red:
                        RightBudgetBar.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                        break;
                }
            }

            // Note: HeaderBudgetPercentText was not defined in XAML — budget display is handled by left/right sidebar text blocks only
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Error refreshing context budget: {Message}", ex.Message);

            // Set fallback text on errors
            if (RightBudgetText != null) RightBudgetText.Text = "Budget unavailable";
            if (ContextBudgetText != null) ContextBudgetText.Text = "Budget unavailable";
        }
    }

    private void OnContextCompressionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Map ComboBox selection to CompressionLevel enum
        if (_selectedChatId == null || _budgeter == null) return;

        var comboBox = (ComboBox)sender!;
        var selectedIndex = comboBox.SelectedIndex;

        var strategy = selectedIndex switch
        {
            0 => Domain.Models.CompressionLevel.None,
            1 => Domain.Models.CompressionLevel.Light,
            2 => Domain.Models.CompressionLevel.Medium,
            3 => Domain.Models.CompressionLevel.Aggressive,
            _ => Domain.Models.CompressionLevel.Medium
        };

        _ = _budgeter.SetCompressionStrategyForChatAsync(_selectedChatId.Value, strategy);
    }

    private void OnRightCompressionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Same as left sidebar but for right sidebar selector
        if (_selectedChatId == null || _budgeter == null) return;

        var comboBox = (ComboBox)sender!;
        var selectedIndex = comboBox.SelectedIndex;

        var strategy = selectedIndex switch
        {
            0 => Domain.Models.CompressionLevel.None,
            1 => Domain.Models.CompressionLevel.Light,
            2 => Domain.Models.CompressionLevel.Medium,
            3 => Domain.Models.CompressionLevel.Aggressive,
            _ => Domain.Models.CompressionLevel.Medium
        };

        _ = _budgeter.SetCompressionStrategyForChatAsync(_selectedChatId.Value, strategy);
    }

    // ---- Custom Context Injection ----

    private void OnInjectCustomContextClicked(object? sender, RoutedEventArgs e)
    {
        // Toggle custom context injection panel visibility (left sidebar version)
        if (CustomContextInjectionPanel != null)
            CustomContextInjectionPanel.IsVisible = !CustomContextInjectionPanel.IsVisible;

        // Also toggle right sidebar panel
        if (RightCustomContextInjectionPanel != null)
            RightCustomContextInjectionPanel.IsVisible = CustomContextInjectionPanel?.IsVisible == true;
    }

    private void OnRightAddCustomContextClicked(object? sender, RoutedEventArgs e)
    {
        // Toggle custom context injection panel visibility from right sidebar button
        if (RightCustomContextInjectionPanel != null)
            RightCustomContextInjectionPanel.IsVisible = !RightCustomContextInjectionPanel.IsVisible;

        // Also toggle left sidebar panel
        if (CustomContextInjectionPanel != null)
            CustomContextInjectionPanel.IsVisible = RightCustomContextInjectionPanel?.IsVisible == true;
    }

    private async void OnRightCustomContextInjectClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextManager == null || _selectedChatId == null) return;

        // Get the injection type from the ComboBox selection (0 = System Prompt custom, 1 = File Contents, 2 = Raw Context)
        var selectedTypeIndex = RightInjectionTypeSelector?.SelectedIndex ?? 0;

        var injectionType = selectedTypeIndex switch
        {
            0 => Domain.Models.ContextInjectionType.CustomInjection,
            1 => Domain.Models.ContextInjectionType.ProjectState,
            _ => Domain.Models.ContextInjectionType.CustomInjection // Fallback: user-defined custom context
        };

        try
        {
            // Inject the custom context and add it to the UI segments list
            var segment = await _contextManager.InjectCustomContextAsync(
                _selectedChatId.Value,
                RightCustomContextContentInput?.Text ?? "",
                injectionType);

            // Add visual representation to the right sidebar segments container
            if (segment != null && RightSegmentsContainer != null)
            {
                var segmentBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var segmentGrid = new Grid();
                segmentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                segmentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Segment label
                var segmentLabel = new TextBlock
                {
                    Text = $"Custom: {injectionType}",
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                    FontSize = 11
                };

                // Remove button — store the segment ID on its Tag property (Avalonia Button DOES support Tag)
                var removeBtn = new Button
                {
                    Content = "✕",
                    Classes = { "msgRemoveCtxBtn" },
                    Padding = new Thickness(6, 2),
                    FontSize = 10,
                    BorderThickness = new Thickness(0),
                    Tag = segment.Id  // ContextSegment.Id is the segment ID — there's no SegmentId property
                };
                removeBtn.Click += OnRemoveCustomContextClicked;

                Grid.SetColumn(segmentLabel, 0);
                Grid.SetColumn(removeBtn, 1);
                segmentGrid.Children.Add(segmentLabel);
                segmentGrid.Children.Add(removeBtn);

                segmentBorder.Child = segmentGrid;
                // Track the Border for later removal (avoids visual tree traversal in Avalonia)
                _customContextBorders[segment.Id] = segmentBorder;
                RightSegmentsContainer.Children.Add(segmentBorder);
            }

            // Collapse the panel after injection
            if (RightCustomContextInjectionPanel != null)
                RightCustomContextInjectionPanel.IsVisible = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to inject custom context");
            ShowError($"Failed to inject custom context: {ex.Message}");
        }
    }

    private async void OnRemoveCustomContextClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextManager == null || _selectedChatId == null) return;

        var button = (Button)sender!;

        // Get segment ID from the button's Tag property — Avalonia Button DOES support Tag properly
        var segmentIdObj = button.Tag as Guid?;
        if (segmentIdObj != null && _selectedChatId.HasValue)
        {
            try
            {
                await _contextManager.RemoveCustomContextAsync(_selectedChatId.Value, segmentIdObj.Value);

                // Remove the visual representation from the UI — custom context borders are direct children of RightSegmentsContainer
                if (_customContextBorders.TryRemove(segmentIdObj.Value, out var borderToRemove))
                {
                    RightSegmentsContainer.Children.Remove(borderToRemove);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to remove custom context");
            }
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
            if (this.Owner is Window ownerWindow)
                new Window { Content = new TextBlock { Text = message } }.ShowDialog(ownerWindow);
            else
                ShowStaticError(message); // Fallback to static method
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Error: {message}");
        }
    }

    /// <summary>
    /// Shows a static error dialog without owner window.
    /// </summary>
    private static void ShowStaticError(string message)
    {
        try
        {
            // Find the first available Window to use as parent (from App.ApplicationServices)
            var appType = typeof(App);
            var propInfo = appType.GetProperty("ApplicationServices",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var serviceProvider = propInfo?.GetValue(null) as IServiceProvider;

            // Try to find the first Window via Avalonia's application lifetime
            if (serviceProvider != null && serviceProvider.GetService<IClassicDesktopStyleApplicationLifetime>() is IClassicDesktopStyleApplicationLifetime appLifetime)
            {
                foreach (var window in appLifetime.Windows)
                {
                    var w = window as Window;
                    if (w != null)
                        new Window { Content = new TextBlock { Text = message } }.ShowDialog(w);
                    return;
                }
            }

            // No parent window found — show without owner
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

            if (App.ApplicationServices != null && App.ApplicationServices.GetService<IClassicDesktopStyleApplicationLifetime>() is IClassicDesktopStyleApplicationLifetime app)
            {
                foreach (var w in app.Windows)
                {
                    var win = w as Window;
                    if (win != null)
                        errorWin.ShowDialog(win);
                    return;
                }
            }

            errorWin.Show();
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Error: {message}");
        }
    }

    // ---- Tab Pointer Pressed Event Handlers ----

    private void OnChatTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Chat");

    private void OnServerTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Server");

    private void OnModelsTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Models");

    private void OnDevicesTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Devices");

    private void OnContextTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Also switch right sidebar to Context tab
        UpdateRightSidebarTab("Context");
        ShowTab("Context");
    }

    private void OnImageGenTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("ImageGen");

    /// <summary>
    /// Handler for the Generate Image button — calls the DiffusionPipelineService to generate an image.
    /// </summary>
    private async void OnImageGenGenerateClicked(object? sender, RoutedEventArgs e)
    {
        if (_diffusionPipeline == null)
        {
            // Resolve from DI
            try
            {
                _diffusionPipeline = GetAppServiceProvider()?.GetService<OpenLMStudio.Application.Interfaces.IDiffusionPipelineService>();
            }
            catch { /* Ignore resolution errors */ }
        }

        if (_diffusionPipeline == null)
        {
            ShowError("Diffusion pipeline not available. Ensure ONNX Runtime and diffusion models are configured.");
            return;
        }

        var prompt = ImageGenPromptInput?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ShowError("Prompt is required for image generation.");
            return;
        }

        var negativePrompt = ImageGenNegPromptInput?.Text ?? string.Empty;

        // Read parameters from UI
        var steps = (int)(ImageGenStepsSlider?.Value ?? 20);
        var cfgScale = (float)(ImageGenCfgSlider?.Value ?? 7.5);
        var seedText = ImageGenSeedInput?.Text;
        var seed = long.TryParse(seedText ?? string.Empty, out var parsedSeed) ? parsedSeed : -1;
        var batchSize = (int)(ImageGenBatchSizeSlider?.Value ?? 1);

        // Get resolution from selector
        var resolutionText = ImageGenResolutionSelector?.SelectedItem?.ToString() ?? "512x512";
        var resolution = ParseResolution(resolutionText);

        // Show progress
        ImageGenProgressText.Text = $"Generating image...";
        ImageGenGenerateBtn.IsEnabled = false;

        try
        {
            // Get the selected model
            var selectedModelItem = ImageGenModelSelector?.SelectedItem as ContentControl;
            var selectedModel = selectedModelItem?.Content as TextBlock;
            var modelMetadata = selectedModel?.Tag as OpenLMStudio.Domain.Models.MultiModalModelMetadata;
            var modelId = modelMetadata?.Id ?? "default";

            var result = await _diffusionPipeline.GenerateImageAsync(
                new Application.Interfaces.ImageGenerationRequest(
                    modelId,
                    prompt,
                    string.IsNullOrWhiteSpace(negativePrompt) ? null : negativePrompt,
                    resolution.Width,
                    resolution.Height,
                    cfgScale,
                    steps,
                    seed),
                CancellationToken.None);

            // Display the generated image
            if (ImageGenOutputArea != null)
            {
                // ImageGenOutputArea is a Border — wrap content in a StackPanel
                var existingPanel = ImageGenOutputArea.Child as StackPanel;
                if (existingPanel != null)
                {
                    existingPanel.Children.Clear();
                }
                else
                {
                    existingPanel = new StackPanel();
                    ImageGenOutputArea.Child = existingPanel;
                }

                // Convert to bitmap and display
                using var ms = new MemoryStream(result.ImageBytes);
                var image = new Avalonia.Media.Imaging.Bitmap(ms);

                var imageControl = new Avalonia.Controls.Image
                {
                    Source = image,
                    Stretch = Avalonia.Media.Stretch.Uniform,
                    MaxHeight = 512
                };
                existingPanel.Children.Add(imageControl);

                // Add metadata text
                var metadataText = new TextBlock
                {
                    Text = $"Seed: {result.Seed} | CFG: {result.GuidanceScale} | Steps: {result.Steps} | Model: {result.ModelId}",
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(170, 170, 170)),
                    FontSize = 10,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                existingPanel.Children.Add(metadataText);
            }

            ImageGenProgressText.Text = "Image generated successfully.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating image");
            ImageGenProgressText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ImageGenGenerateBtn.IsEnabled = true;
        }
    }

    private static (int Width, int Height) ParseResolution(string resolutionText)
    {
        try
        {
            var parts = resolutionText.Replace("x", "x").Split('x');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }
        catch
        {
            return (512, 512);
        }
    }

    /// <summary>
    /// Attaches Click event handlers to the Tab TextBlocks so users can switch tabs by clicking.
    /// </summary>
    private void AttachTabClickHandlers()
    {
        // Each tab's title TextBlock is inside a StackPanel — attach click to that panel instead for better hit target
        var tabPanels = new[] { ChatTabContent, ServerTabContent, ModelsTabContent, DevicesTabContent, ContextTabContent };
        foreach (var tab in tabPanels)
        {
            if (tab == null) continue;

            // Make the entire StackPanel clickable by attaching a Click handler to its first element
            var child = tab.Children.OfType<Control>().FirstOrDefault();
            if (child != null && !string.IsNullOrEmpty(tab.Name))
            {
                try
                {
                    switch (tab.Name)
                    {
                        case "ChatTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Chat"); break;
                        case "ServerTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Server"); break;
                        case "ModelsTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Models"); break;
                        case "DevicesTabContent":
                            child.PointerPressed += (_, _) => ShowTab("Devices"); break;
                        case "ContextTabContent":
                            child.PointerPressed += (_, _) => { UpdateRightSidebarTab("Context"); ShowTab("Context"); }; break;
                    }
                }
                catch { /* Ignore errors on individual tab attaches */ }
            }
        }

        // Also attach click handlers directly to the TabControl buttons in XAML for reliability — use lambda instead of RoutedEventHandler
        if (ChatTabContent?.Children.OfType<Control>().FirstOrDefault() is Control chatClickTarget)
            chatClickTarget.PointerPressed += (_, _) => ShowTab("Chat");

        var serverChild = ServerTabContent?.Children.OfType<Control>().FirstOrDefault();
        serverChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Server"));

        var modelsChild = ModelsTabContent?.Children.OfType<Control>().FirstOrDefault();
        modelsChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Models"));

        var devicesChild = DevicesTabContent?.Children.OfType<Control>().FirstOrDefault();
        devicesChild?.AddHandler(Control.PointerPressedEvent, (_, _) => ShowTab("Devices"));

        // Attach right sidebar tab button click handlers
        if (RightContextTabButton != null)
            RightContextTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightContextTabButton.IsChecked == true ? "Context" : _activeTab);

        if (RightServerTabButton != null)
            RightServerTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightServerTabButton.IsChecked == true ? "Server" : _activeTab);

        if (RightDevicesTabButton != null)
            RightDevicesTabButton.IsCheckedChanged += (_, _) => UpdateRightSidebarTab(RightDevicesTabButton.IsChecked == true ? "Devices" : _activeTab);
    }

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

    // ---- Image Generation Event Handlers ----

    /// <summary>
    /// Handler for the Image Generation model selector — discovers available diffusion/VAE models from the repository.
    /// </summary>
    private async void OnImageGenModelSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_modelRepository == null || _selectedChatId == null) return;

        try
        {
            // Use SearchMultiModalModelsAsync to get image generation / diffusion / VAE models
            var relevantModels = await _modelRepository.SearchMultiModalModelsAsync(
                modelTypeFilter: Domain.Models.ModelType.ImageGeneration);

            // Also include Diffusion and VAE types — we need both in the dropdown
            var allMultiModalModels = (await _modelRepository.ListMultiModalModelsAsync()).ToList();
            var imageGenModels = relevantModels.ToList().Concat(
                allMultiModalModels.Where(m =>
                    new[] { Domain.Models.ModelType.Diffusion, Domain.Models.ModelType.Vae }.Contains(m.ModelType))
            ).DistinctBy(m => m.Id).ToList();

            // Populate the dropdown with available models
            if (ImageGenModelSelector != null)
            {
                ImageGenModelSelector.Items.Clear();

                foreach (var model in imageGenModels)
                {
                    var sizeStr = model.FileSizeBytes > 0
                        ? $"{model.FileSizeBytes / 1_048_576:F0} MB"
                        : "N/A";

                    var item = new TextBlock
                    {
                        Text = $"{model.Name} ({sizeStr})",
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        Padding = new Thickness(8)
                    };

                    // Store the model metadata for later lookup on selection
                    item.Tag = model;
                    ImageGenModelSelector.Items.Add(item);
                }

                if (imageGenModels.Any())
                {
                    // Auto-select the first model and update resolution based on its default resolution
                    var firstItem = ImageGenModelSelector.Items[0] as ContentControl;
                    MultiModalModelMetadata? selectedModel = null;

                    if (firstItem != null && firstItem.Content is TextBlock txt)
                        selectedModel = txt.Tag as MultiModalModelMetadata;

                    ImageGenModelSelector.SelectedIndex = 0;

                    // Update resolution selector based on selected model's default resolution
                    if (selectedModel?.DefaultResolution != null && selectedModel.DefaultResolution > 0)
                    {
                        UpdateDefaultResolution(selectedModel.DefaultResolution.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to discover image generation models");
        }
    }

    /// <summary>
    /// Updates the resolution dropdown based on a model's default resolution.
    /// </summary>
    private void UpdateDefaultResolution(int defaultRes)
    {
        if (ImageGenResolutionSelector == null || defaultRes <= 0) return;

        // Find existing item that matches and select it, or keep current selection
        foreach (var item in ImageGenResolutionSelector.Items.OfType<ContentControl>())
        {
            if (item.Content is TextBlock tb && tb.Text?.Contains($"{defaultRes}") == true)
            {
                ImageGenResolutionSelector.SelectedItem = item;
                return;
            }
        }

        // If no matching item found, add the default resolution to the list
        var newItem = new TextBlock
        {
            Text = $"{defaultRes}x{defaultRes}",
            Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
            Padding = new Thickness(8)
        };

        ImageGenResolutionSelector.Items.Add(newItem);
        ImageGenResolutionSelector.SelectedIndex = ImageGenResolutionSelector.Items.Count - 1;
    }

    /// <summary>
    /// Handler for the random seed button — generates a random seed value.
    /// </summary>
    private void OnRandomSeedClicked(object? sender, RoutedEventArgs e)
    {
        var rng = new Random();
        if (ImageGenSeedInput != null)
            ImageGenSeedInput.Text = rng.Next(int.MinValue, int.MaxValue).ToString();
    }

    // ---- Settings Window ----

    /// <summary>
    /// Handler for the refresh devices button — re-reads device state from hardware.
    /// </summary>
    private async void OnRefreshDevicesClicked(object? sender, RoutedEventArgs e)
    {
        _ = UpdateDeviceStatusAsync();
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWin = new SettingsWindow();

            if (Owner is Window ownerWindow)
                settingsWin.ShowDialog(ownerWindow);
            else
                settingsWin.Show();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open settings window");
            ShowStaticError($"Failed to open settings: {ex.Message}");
        }
    }

    // ---- Keyboard Shortcuts ----

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+N: New Chat
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.N)
        {
            OnNewChatClicked(null, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl+M: Toggle Model List
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.M)
        {
            ShowTab("Models");
            e.Handled = true;
            return;
        }

        // Ctrl+S: Toggle Server
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            OnServerStartStopClicked(null, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl+K: Toggle Context Panel
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.K)
        {
            ShowTab("Context");
            UpdateRightSidebarTab("Context");
            e.Handled = true;
            return;
        }

        // Ctrl+L: Settings
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.L)
        {
            OnSettingsClicked(null, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
    }

    // ---- Cleanup on window close ----

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Unsubscribe from server state changes before the window is closed
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged -= OnServerStateChanged;

        // Dispose context manager if it implements IDisposable
        _contextManager?.Dispose();
    }

}