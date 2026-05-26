// Avalonia Window code-behind — Streaming response partial class
// Brought to you by Carls' Jr.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Services;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
    // ---- Assistant Response Handling (with streaming token-by-token support) ----

    private IMarkdownRenderer? _markdownRenderer;

    private async Task GetAssistantResponseAsync(Guid chatId, string userMessage)
    {
        if (_conversationManager == null || _selectedChatId != chatId) return;

        // Resolve markdown renderer lazily
        if (_markdownRenderer == null)
        {
            var sp = GetAppServiceProvider();
            _markdownRenderer = sp?.GetService(typeof(IMarkdownRenderer)) as IMarkdownRenderer;
        }

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
        var inDataEvent = false;
        while (!reader.EndOfStream && _isStreaming)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            // SSE format: blank lines separate events
            if (inDataEvent && line.Length > 0)
            {
                // Accumulate multi-line data events
                currentData.Append(line);
                continue;
            }

            // Process the accumulated event
            var dataStr = currentData.ToString();
            if (!string.IsNullOrEmpty(dataStr))
            {
                if (dataStr == "[DONE]") break; // Stream complete

                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(dataStr);
                    if (jsonDoc.RootElement.TryGetProperty("token", out var tokenElement))
                    {
                        var tokenValue = tokenElement.GetString();
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_assistantTextBlock != null)
                            {
                                _assistantTextBlock.Text += (tokenValue ?? "");
                                ScrollToBottomAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                            }
                        });

                        // Render accumulated markdown after each token (when renderer is available)
                        if (_markdownRenderer != null && _assistantTextBlock?.Text != null)
                        {
                            var rendered = _markdownRenderer.Render(_assistantTextBlock.Text);
                            _assistantTextBlock.Text = rendered;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore parsing errors for non-textual JSON events (e.g., usage stats)
                    _logger?.LogDebug("SSE parsing error: {Message}", ex.Message);
                }
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

        // Pass Domain.Models.Message directly to ChatRequest (ChatRequest expects List<Domain.Models.Message>)
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

                // Render accumulated markdown after each token
                if (_markdownRenderer != null && _assistantTextBlock?.Text != null)
                {
                    var rendered = _markdownRenderer.Render(_assistantTextBlock.Text);
                    _assistantTextBlock.Text = rendered;
                }
            }
            catch
            {
                // Ignore non-text events like usage stats, errors, etc.
            }
        }

        // Final markdown render after stream completes
        if (_markdownRenderer != null && _assistantTextBlock?.Text != null)
        {
            var rendered = _markdownRenderer.Render(_assistantTextBlock.Text);
            _assistantTextBlock.Text = rendered;
        }

        // Note: User message was already added to the conversation in OnSendMessageClicked — do NOT add again
        // Update token count after stream completes
        var totalTokens = await _conversationManager.CalculateTotalTokenCountAsync(chatId);
        TokenCountText.Text = $"Tokens: {totalTokens}";
    }
}