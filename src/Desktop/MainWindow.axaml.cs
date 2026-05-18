// Avalonia Window code-behind — converts WPF-specific types to Avalonia equivalents

using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

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

     /// <summary>Flag indicating whether a streaming response is in progress.</summary>
     private bool _isStreaming = false;

     /// <summary>The current assistant message border being streamed into during an active SSE session.</summary>
     private Border? _currentAssistantBorder;

     /// <summary>The text block within the assistant message that receives streamed tokens.</summary>
     private TextBlock? _assistantTextBlock;

    // Tab tracking
    private string _activeTab = "Chat";
    private Guid? _selectedChatId;

    /// <summary>
    /// Creates the main window with pre-resolved dependencies from the application's DI container.
    /// </summary>
    public MainWindow(
        ILogger<MainWindow>? logger,
        IConversationManager? conversationManager = null,
        IServerService? serverService = null,
        IModelRepository? modelRepository = null)
    {
        InitializeComponent();

        // Set window title programmatically to avoid XAML entity reference issues with "&" character
        this.Title = "OpenLMStudio - Local LLM Server & Chat Client";

        _logger = logger;

         // Use pre-resolved dependencies from App.OnStartup — if none are provided (for testing), fall back to DI resolution attempt.
         _conversationManager = conversationManager ?? ResolveConversationManagerFromAppServices();
         _serverService = serverService ?? ResolveServerServiceFromAppServices();
         _modelRepository = modelRepository ?? ResolveModelRepositoryFromAppServices();
         _chatCompletionService = chatCompletionService ?? ResolveChatCompletionServiceFromAppServices();

         // Subscribe to server state changes
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged += OnServerStateChanged;

        // Set up event handlers for UI interactions
        SetupEventHandlers();

        // Load tab click handlers (they need access to this instance's ShowTab method)
        AttachTabClickHandlers();

        RefreshChatListAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // ---- UI Event Handlers Setup ----

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
    }

    // ---- Tab Navigation ----

    private void ShowTab(string tabName)
    {
        _activeTab = tabName;

        // Hide all tab contents first
        SetTabVisibility(ChatTabContent, false);
        SetTabVisibility(ServerTabContent, false);
        SetTabVisibility(ModelsTabContent, false);
        SetTabVisibility(DevicesTabContent, false);
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

        foreach (var tb in tabs)
        {
            if (tb == null) continue;

            // Only update the first TextBlock of each tab section (the tab title)
            var parent = tb.Parent as Panel;
            if (parent?.Name != null &&
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase))
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

        // Load the selected conversation's messages (fire-and-forget since OnChatItemClicked is async void)
        _ = LoadConversationMessagesAsync(chatIdObj.Value);
    }

    private async void OnNewChatClicked(object? sender, RoutedEventArgs e)
    {
        if (_conversationManager == null) return;

        var newChat = await _conversationManager.CreateChatAsync("New Chat");
        RefreshChatListAsync();

        // Automatically select the new chat (fire-and-forget since this is async void)
        _ = LoadConversationMessagesAsync(newChat.Id);
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

            // Display each message with alternating styling
            foreach (var msg in messages)
            {
                var messageBorder = CreateMessageBorder(msg);
                if (messageBorder != null)
                    MessageDisplayPanel?.Children.Add(messageBorder);
            }

            // Update chat title display
            ChatTitleText.Text = "Chat Session";

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
            // Add role label for user/system messages
            var stackPanel = new StackPanel();

            var roleLabel = new TextBlock
            {
                Text = message.Role.ToString().ToUpper(),
                Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 8, 4)
            };

            if (message.Role == MessageRole.User)
                roleLabel.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green for user

            stackPanel.Children.Add(roleLabel);
            stackPanel.Children.Add(textBlock);

            border.Child = stackPanel;
        }

        return border;
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

                    // Get available memory
                    try
                    {
                        var ramAvailable = GC.GetGCMemoryInfo().HeapSizeBytes / 1073741824;
                        RamInfoText.Text = $"RAM: {ramAvailable:F0} GB Available";
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
            if (models.Any())
            {
                // TODO: Add model items to the panel
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
            catch (Exception serverEx) when (serverEx is IOException or TaskCanceledException)
            {
                // Server not available — fall back to local chat completion service
                _logger?.LogDebug("Server streaming failed, falling back to local IChatCompletionService: {Message}", serverEx.Message);
            }

            if (!usedServerEndpoint && _chatCompletionService != null)
            {
                await StreamResponseViaLocalServiceAsync(chatId, userMessage);
                usedServerEndpoint = true;
            }

            // Stop streaming indicator regardless of how the response was generated
            StreamingIndicator.IsVisible = false;
            _isStreaming = false;
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

        var chatMessages = await _conversationManager.GetMessagesAsync(chatId) ?? [];
        var userMsg = new Message { Role = MessageRole.User, Content = userMessage };
        chatMessages.Add(userMsg);

        var requestBody = new ChatCompletionRequest("default", chatMessages.ToList()) { Stream = true };

        var response = await httpClient.PostAsync(uri, new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Server returned: {response.StatusCode}");

        // Read SSE stream token-by-token and update UI on each event
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        string? lineBuffer = null;
        StringBuilder currentData = new();
        bool inDataEvent = false;
        while (!reader.EndOfStream && _isStreaming)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            // Parse SSE event format: "data: {\"token\": \"...\", ...}" or "data: [DONE]"
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                inDataEvent = true;
                currentData.Append(line.Substring(5).Trim());
            }

            // SSE events are separated by blank lines
            if (!inDataEvent || line.Length > 0) continue;

            try
            {
                var dataStr = currentData.ToString();
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

            inDataEvent = false;
            currentData.Clear();
        }

        await _conversationManager.AddMessageAsync(chatId, userMsg);

        // Update token count after stream completes
        var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
        TokenCountText.Text = $"Tokens: {totalTokens}";
    }

    /// <summary>
    /// Streams a chat completion response via the local IChatCompletionService.
    /// </summary>
    private async Task StreamResponseViaLocalServiceAsync(Guid chatId, string userMessage)
    {
        var chatMessages = await _conversationManager.GetMessagesAsync(chatId);
        if (chatMessages == null || !chatMessages.Any()) throw new InvalidOperationException("No messages to send.");

        // Use the last assistant message as the model ID fallback — in practice this would be selected by user.
        var modelId = "default"; // TODO: Get from a model selector UI element

        await foreach (var chunk in _chatCompletionService.GetStreamingCompletionAsync(
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
                        ScrollToBottomAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                });
            }
            catch
            {
                // Ignore non-text events like usage stats, errors, etc.
            }
        }

        await _conversationManager.AddMessageAsync(chatId, new Message { Role = MessageRole.User, Content = userMessage });

        // Update token count after stream completes
        var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
        TokenCountText.Text = $"Tokens: {totalTokens}";
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

    /// <summary>
    /// Attaches Click event handlers to the Tab TextBlocks so users can switch tabs by clicking.
    /// </summary>
    private void AttachTabClickHandlers()
    {
        // Each tab's title TextBlock is inside a StackPanel — attach click to that panel instead for better hit target
        var tabPanels = new[] { ChatTabContent, ServerTabContent, ModelsTabContent, DevicesTabContent };
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

    private T? FindChild<T>(Panel parent, int maxDepth = 10) where T : Control
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

    /// <summary>
    /// Handler for ImageGenModelSelector SelectionChanged event.
    /// Updates the selected image generation model based on user selection.
    /// </summary>
    private void OnImageGenModelSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // TODO: Implement actual image generation model selection logic
    }

    // ---- Tab Pointer Pressed Event Handlers ----

    private void OnChatTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Chat");

    private void OnServerTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Server");

    private void OnModelsTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Models");

    private void OnDevicesTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) => ShowTab("Devices");

    private void OnImageGenTabPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // ImageGen tab is not currently managed by the main tab system — show a placeholder message
        ShowError("Image generation support requires diffusion engine integration (Phase 3).");
    }

}
