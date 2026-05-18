using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// Manages the main application window including chat list, conversation display, server controls, and tab navigation.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ILogger<MainWindow>? _logger;
    private readonly IConversationManager? _conversationManager;
    private readonly IServerService? _serverService;
    private readonly IModelRepository? _modelRepository;
    
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

        _logger = logger;
        
        // Use pre-resolved dependencies from App.OnStartup — if none are provided (for testing), fall back to DI resolution attempt.
        _conversationManager = conversationManager ?? ResolveConversationManagerFromAppServices();
        _serverService = serverService ?? ResolveServerServiceFromAppServices();
        _modelRepository = modelRepository ?? ResolveModelRepositoryFromAppServices();

        // Subscribe to server state changes
        if (_serverService is OpenLMStudio.Infrastructure.Services.ServerService realSvc)
            realSvc.StateChanged += OnServerStateChanged;

        // Set up event handlers for UI interactions
        SetupEventHandlers();
        
        // Load tab click handlers (they need access to this instance's ShowTab method)
        AttachTabClickHandlers();
        
        RefreshChatListAsync();
    }

    /// <summary>
    /// Event fired when a property changes on the main window view model.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ---- UI Event Handlers Setup ----

    private void SetupEventHandlers()
    {
        // New chat button
        if (NewChatButton != null)
            NewChatButton.Click += OnNewChatClicked;

        // Send message button
        if (SendButton != null)
            SendButton.Click += OnSendMessageClicked;

        // Server start/stop button
        if (ServerStartStopButton != null)
            ServerStartStopButton.Click += OnServerStartStopClicked;

        // Handle Enter key in input box for sending messages
        if (MessageInputBox != null)
            MessageInputBox.KeyDown += OnMessageInputKeyDown;

        // Add click handlers to tab TextBlocks (they're not exposed as fields so we find them by name)
        var allTextBlocks = FindChildren<TextBlock>(ChatTabContent).ToList();
    }

    // ---- Tab Navigation ----

    private void ShowTab(string tabName)
    {
        _activeTab = tabName;
        
        // Hide all tab contents first
        ChatTabContent.Visibility = Visibility.Collapsed;
        ServerTabContent.Visibility = Visibility.Collapsed;
        ModelsTabContent.Visibility = Visibility.Collapsed;
        DevicesTabContent.Visibility = Visibility.Collapsed;

        // Show the selected tab content
        switch (tabName)
        {
            case "Chat":
                ChatTabContent.Visibility = Visibility.Visible;
                break;
            case "Server":
                ServerTabContent.Visibility = Visibility.Visible;
                UpdateServerStatus();
                break;
            case "Models":
                ModelsTabContent.Visibility = Visibility.Visible;
                RefreshModelListAsync();
                break;
            case "Devices":
                DevicesTabContent.Visibility = Visibility.Visible;
                UpdateDeviceStatus();
                break;
        }

        // Update active tab styling
        UpdateActiveTab(tabName);
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
            var parent = tb.Parent as FrameworkElement;
            if (parent?.Name != null && 
                new[] { "ChatTabContent", "ServerTabContent", "ModelsTabContent", "DevicesTabContent" }
                    .Contains(parent.Name))
            {
                if (activeTabName.Equals(tb.Text, StringComparison.OrdinalIgnoreCase))
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247)); // AccentBlue
                    tb.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // TextPrimary
                    tb.FontWeight = FontWeights.Normal;
                }
            }
        }

        OnPropertyChanged(nameof(_activeTab));
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
        var button = new Button
        {
            Content = chat.Name ?? $"Conversation {chat.Id.ToString("N").Substring(0, 8)}",
            Style = (Style)FindResource("ChatItemButton"),
            Tag = chat.Id,
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Highlight active/selected chat (null-checked for CS8602)
        if (_selectedChatId != null && _selectedChatId.Value == chat.Id)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        }

        // Add token count as tooltip - _conversationManager already verified non-null in caller path (CreateChatListItem is only called after ListChatsAsync which requires it)
#pragma warning disable CS8602 // Dereference of a possibly null reference
        var tokenCount = _conversationManager.CalculateTotalTokenCountAsync(chat.Id).GetAwaiter().GetResult();
#pragma warning restore CS8602
        var toolTip = new System.Windows.Controls.ToolTip();
        toolTip.Content = $"Tokens: {tokenCount}";
        button.ToolTip = toolTip;

        button.Click += OnChatItemClicked;
        return button;
    }

    private async void OnChatItemClicked(object sender, RoutedEventArgs e)
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
                child.Background = FindResource("ChatItemButton") is Style s 
                    ? (s.Setters.Cast<SetterBase>().OfType<Setter>()
                        .First(x => x.Property == Border.BackgroundProperty).Value as Brush) ?? fallback
                    : fallback;
            }
        }

        // Load the selected conversation's messages (fire-and-forget since OnChatItemClicked is async void)
        _ = LoadConversationMessagesAsync(chatIdObj.Value);
    }

    private async void OnNewChatClicked(object sender, RoutedEventArgs e)
    {
        if (_conversationManager == null) return;

        var newChat = await _conversationManager.CreateChatAsync("New Chat");
        RefreshChatListAsync();
        
        // Automatically select the new chat (fire-and-forget since this is async void)
        _ = LoadConversationMessagesAsync(newChat.Id);
    }

    // ---- Conversation Message Loading and Display ----

    // Note: This is async Task (not async void) so it can be awaited by callers.
    // It's invoked programmatically from OnChatItemClicked, not directly by the WPF event system.
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
            if (MessageScrollViewer != null)
                MessageScrollViewer.ScrollToBottom();
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
        }
        else // Assistant or Tool
        {
            border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            border.CornerRadius = new CornerRadius(0, 8, 8, 8);
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
                FontWeight = FontWeights.SemiBold,
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
                FontWeight = FontWeights.SemiBold,
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

    private async void OnServerStartStopClicked(object sender, RoutedEventArgs e)
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
                ServerStartStopButton.Content = "Start Server";
                ServerStatusText.Text = "Server: Stopped";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            }
            else
            {
                // Start the server on a default port
                var configuration = new ServerConfiguration { Port = 8080 };
                await _serverService.StartAsync(configuration);

                // Update UI to reflect running state
                ServerStartStopButton.Content = "Stop Server";
                ServerStatusText.Text = $"Server: Running (Port {configuration.Port})";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }

            UpdateServerStatus();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error toggling server state");
            MessageBox.Show($"Server error: {ex.Message}", "OpenLMStudio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateServerStatus()
    {
        if (_serverService == null) return;

        var isRunning = _serverService.State != ServerState.Stopped;

        // Update server status display across all UI elements
        ServerStatusText.Text = $"Server: {(isRunning ? "Running" : "Stopped")}";
        
        ServerStartStopButton.Content = isRunning ? "Stop Server" : "Start Server";

        if (isRunning)
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            
            // Try to get port from the server service's configuration
            var srv = _serverService as OpenLMStudio.Infrastructure.Services.ServerService;
            if (srv?.Configuration != null)
            {
                ServerPortRightText.Text = $"Port: {srv.Configuration.Port}";
                ServerPortText.Text = $"Port: {srv.Configuration.Port}";
            }
        }
        else
        {
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
            ServerPortRightText.Text = "Port: 8080 (default)";
            ServerPortText.Text = "Port: 8080 (default)";
        }
    }

    private void OnServerStateChanged(object? sender, ServerStateChangedEventArgs e)
    {
        // Update UI on server state changes from the service itself
        Dispatcher.Invoke(() => UpdateServerStatus());
        
        if (e.NewState == ServerState.Error && !string.IsNullOrEmpty(e.Message))
        {
            Dispatcher.Invoke(() => 
                MessageBox.Show($"Server error: {e.Message}", "OpenLMStudio", MessageBoxButton.OK, MessageBoxImage.Warning));
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
            var parentPanel = ModelsTabContent?.Parent as DependencyObject;
            if (parentPanel != null)
            {
                foreach (var child in FindChildren<StackPanel>(parentPanel).ToList())
                    ((FrameworkElement)child).Visibility = Visibility.Collapsed;
            }

            if (!models.Any())
            {
                // Show "No models" message in the tab's StackPanel directly
                var textBlock = new TextBlock
                {
                    Text = "No models loaded",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Padding = new Thickness(12),
                    FontSize = 12
                };

                // Find the existing scrollviewer's content stackpanel and add to it
                var _scrollViewer = ModelsTabContent?.FindName("ScrollViewer");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing model list");
        }
    }

    // ---- Message Sending ----

    private async void OnSendMessageClicked(object sender, RoutedEventArgs e)
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
            _ = Task.Run(async () => {
                if (_conversationManager != null && _selectedChatId.HasValue)
                    await GetAssistantResponseAsync(_selectedChatId.Value, userMessage.Content!);
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending message");
            MessageBox.Show($"Failed to send message: {ex.Message}", "OpenLMStudio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnMessageInputKeyDown(object sender, KeyEventArgs e)
    {
        // Send on Enter (without Shift for multi-line), or Ctrl+Enter always
        if (e.Key == Key.Return && (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)))
        {
            OnSendMessageClicked(sender, e);
        }
    }

    // ---- Assistant Response Handling ----

    private async Task GetAssistantResponseAsync(Guid chatId, string userMessage)
    {
        if (_conversationManager == null || _selectedChatId != chatId) return;

        try
        {
            var assistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Content = "[Generating response...]",
                TokenCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            // Display placeholder response
            Border? assistantBorder = CreateMessageBorder(assistantMessage);
            if (assistantBorder != null)
                MessageDisplayPanel?.Children.Add(assistantBorder);

            if (MessageScrollViewer != null)
                MessageScrollViewer.ScrollToBottom();

            _logger?.LogInformation("Getting assistant response for: {ChatId}", chatId);

            // TODO: Wire up to actual IChatCompletionService when available via DI
            // For now, show a placeholder response
            if (assistantBorder != null)
                assistantBorder.Child = new TextBlock
            {
                Text = "Assistant response requires IChatCompletionService integration.\n\nTo enable real responses:\n1. Install llama.cpp native bindings (libllama.dll)\n2. Configure in appsettings.json: \"Inference\": { \"Backend\": \"llama-cpp\" }",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 14,
                Margin = new Thickness(0)
            };

            // Update token count display
            var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
            TokenCountText.Text = $"Tokens: {totalTokens}";

            await _conversationManager.AddMessageAsync(chatId, assistantMessage);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting assistant response");
        }
    }

    // ---- Utility Methods ----

    private int EstimateTokenCount(string text) => 
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + 3) / 4);

    /// <summary>
    /// Gets the chat scroll viewer for scrolling to bottom after adding messages.
    /// </summary>
    private ScrollViewer? MessageScrollViewer
    {
        get
        {
            var parent = MessageDisplayPanel?.Parent as DependencyObject;
            return parent != null ? FindChild<ScrollViewer>(parent) : null;
        }
    }

    // ---- Helper Methods ----

    /// <summary>
    /// Attempts to get the application's DI service provider from App.ApplicationServices.
    /// </summary>
    private static IServiceProvider? GetAppServiceProvider()
    {
        var appType = typeof(App).Assembly.CreateInstance("OpenLMStudio.Desktop.App");
        if (appType == null) return null;

        try
        {
            // Try to access ApplicationServices property via reflection
            var propInfo = (appType as System.Type)?.GetProperty("ApplicationServices", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return propInfo?.GetValue(null) as IServiceProvider;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = (T?)VisualTreeHelper.GetChild(parent, i);
            if (child != null)
            {
                yield return child;
                foreach (var descendant in FindChildren<T>(child))
                    yield return descendant;
            }
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
            var child = tab.Children.OfType<UIElement>().FirstOrDefault();
            if (child != null && !string.IsNullOrEmpty(tab.Name))
            {
                try
                {
                    switch (tab.Name)
                    {
                        case "ChatTabContent":
                            child.MouseLeftButtonUp += (_, _) => ShowTab("Chat"); break;
                        case "ServerTabContent":
                            child.MouseLeftButtonUp += (_, _) => ShowTab("Server"); break;
                        case "ModelsTabContent":
                            child.MouseLeftButtonUp += (_, _) => ShowTab("Models"); break;
                        case "DevicesTabContent":
                            child.MouseLeftButtonUp += (_, _) => ShowTab("Devices"); break;
                    }
                }
                catch { /* Ignore errors on individual tab attaches */ }
            }
        }

        // Also attach click handlers directly to the TabControl buttons in XAML for reliability
        if (ChatTabContent?.Children.OfType<UIElement>().FirstOrDefault() is UIElement chatClickTarget)
            chatClickTarget.MouseLeftButtonUp += (_, _) => ShowTab("Chat");
        
        var serverChild = ServerTabContent?.Children.OfType<UIElement>().FirstOrDefault();
        serverChild?.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => ShowTab("Server")));

        var modelsChild = ModelsTabContent?.Children.OfType<UIElement>().FirstOrDefault();
        modelsChild?.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => ShowTab("Models")));

        var devicesChild = DevicesTabContent?.Children.OfType<UIElement>().FirstOrDefault();
        devicesChild?.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => ShowTab("Devices")));
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

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = (T?)VisualTreeHelper.GetChild(parent, i);
            if (child != null) return child;
        }

        return null;
    }
}