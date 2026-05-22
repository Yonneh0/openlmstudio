// Avalonia Window code-behind — Chat messages partial class
// Brought to you by Carls' Jr.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.Pingu;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
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
        button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;

        // Highlight active/selected chat (null-checked for CS8602)
        if (_selectedChatId != null && _selectedChatId.Value == chat.Id)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        }

        // Use Avalonia's ToolTip.SetTip() attached method instead of Tooltip property
        // Defer token count calculation to avoid blocking the UI thread
        var toolTipText = new TextBlock { Text = "Loading..." };
        if (button.Parent is Border buttonBorder)
            ToolTip.SetTip(button, toolTipText);
        else
            button.AttachedToVisualTree += (_, _) =>
            {
                var p = button.Parent as Border;
                if (p != null && !(toolTipText.Parent is Panel))
                    ToolTip.SetTip(p, toolTipText);
            };

        // Load token count asynchronously without blocking the UI thread
        var chatIdForToken = chat.Id;
        var mgrForToken = _conversationManager;
        _ = Task.Run(async () =>
        {
            try
            {
                if (mgrForToken != null)
                {
                    var count = await mgrForToken.CalculateTotalTokenCountAsync(chatIdForToken);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        toolTipText.Text = $"Tokens: {count}";
                    });
                }
            }
            catch { /* Ignore token count errors */ }
        });

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

        string displayContent = message.Content ?? "";

        // Render markdown for assistant messages when renderer is available
        if (_markdownRenderer != null && message.Role == MessageRole.Assistant)
        {
            try
            {
                displayContent = _markdownRenderer.Render(displayContent);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Markdown rendering failed for message {MessageId}: {Message}", message.Id, ex.Message);
                // Fall through with raw content
            }
        }

        var textBlock = new TextBlock
        {
            Text = displayContent,
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

            // Render ImageOutput(s) for image generation responses
            if (message.ImageOutputs?.Any() == true)
            {
                foreach (var img in message.ImageOutputs)
                {
                    try
                    {
                        byte[] imageBytes;
                        if (img.ImageData.Length < 1024 && !img.ImageData.Contains(',') && img.ImageData.All(b => b >= 32 && b < 128))
                        {
                            // It's a base64-encoded PNG string
                            imageBytes = Convert.FromBase64String(img.ImageData);
                        }
                        else
                        {
                            // It's already a byte array serialized as a string
                            imageBytes = Encoding.UTF8.GetBytes(img.ImageData);
                        }
                        using var ms = new MemoryStream(imageBytes);
                        var image = new Avalonia.Media.Imaging.Bitmap(ms);

                        var imageControl = new Avalonia.Controls.Image
                        {
                            Source = image,
                            Stretch = Avalonia.Media.Stretch.Uniform,
                            MaxHeight = 512,
                            Margin = new Thickness(0, 8, 0, 0)
                        };
                        outerStackPanel.Children.Add(imageControl);

                        // Image metadata
                        var imgMeta = new TextBlock
                        {
                            Text = $"Seed: {img.Seed} | CFG: {img.CfgScale} | Steps: {img.Steps} | {img.Width}x{img.Height} | Model: {img.ModelId}",
                            Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                            FontSize = 10,
                            Margin = new Thickness(0, 4, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        };
                        outerStackPanel.Children.Add(imgMeta);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to render image output for message {MessageId}", message.Id);
                    }
                }
            }

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
}