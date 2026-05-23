// Avalonia Window code-behind — Context management partial class
// Brought to you by Carls' Jr.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Domain.Models.Pingu;

namespace OpenLMStudio.Desktop;

public partial class MainWindow
{
    // ---- Compressed Context Segments Rendering ----

    /// <summary>
    /// Renders compressed context segments from the context manager into the Context sidebar panels.
    /// Called whenever a chat is loaded or context budget changes.
    /// </summary>
    private async Task RefreshCompressedSegmentsAsync()
    {
        if (_selectedChatId == null || _contextManager == null) return;

        try
        {
            var context = await _contextManager.GetCompressedContextAsync(_selectedChatId.Value, CompressionLevel.Medium);

            // Clear existing compressed segments
            CompressedSegmentsContainer?.Children.Clear();

            // Filter out pinned/system/task segments — show only regular message segments that were compressed
            var regularSegments = context.Segments
                .Where(s => s.InjectionType != ContextInjectionType.SystemPrompt
                         && s.InjectionType != ContextInjectionType.TaskContextSnapshot
                         && s.InjectionType != ContextInjectionType.ProjectState)
                .ToList();

            if (!regularSegments.Any())
            {
                // Show placeholder
                if (CompressedSegmentsContainer != null)
                {
                    CompressedSegmentsContainer.Children.Add(new TextBlock
                    {
                        Text = "No compressed segments",
                        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                        Padding = new Thickness(12, 8),
                        FontSize = 10
                    });
                }
            }

            foreach (var segment in regularSegments)
            {
                // Left sidebar compressed segment
                var leftSegmentBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var leftGrid = new Grid();
                leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftLabel = new TextBlock
                {
                    Text = segment.Content != null && segment.Content.Length > 100
                        ? segment.Content[..100] + "..."
                        : segment.Content ?? "",
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                    FontSize = 10
                };

                // Compression indicator
                var indicatorText = segment.IsPinned ? "🟢 Uncompressed (pinned)"
                    : segment.Content != null && segment.Content.Length == 0 ? "🔴 Evicted"
                    : "🟡 Compressed";
                var indicatorBorder = new Border
                {
                    Background = segment.IsPinned ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                        : segment.Content != null && segment.Content.Length == 0 ? new SolidColorBrush(Color.FromRgb(244, 67, 54))
                        : new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(6, 1)
                };
                indicatorBorder.Child = new TextBlock
                {
                    Text = indicatorText,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255))
                };

                Grid.SetColumn(leftLabel, 0);
                Grid.SetColumn(indicatorBorder, 1);
                leftGrid.Children.Add(leftLabel);
                leftGrid.Children.Add(indicatorBorder);
                leftSegmentBorder.Child = leftGrid;

                CompressedSegmentsContainer?.Children.Add(leftSegmentBorder);

                // Message segment with pin/suppress controls (added to left panel's CompressedSegmentsContainer)
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

                var segmentLabel = new TextBlock
                {
                    Text = segment.Content != null && segment.Content.Length > 80
                        ? segment.Content[..80] + "..."
                        : segment.Content ?? "",
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                    FontSize = 10
                };

                // Pin/suppress controls
                var controlStack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
                var pinBtn = new Button { Content = "📌", Classes = { "msgPinBtn" }, Padding = new Thickness(4, 1), FontSize = 9, BorderThickness = new Thickness(0) };
                pinBtn.Tag = segment.Id;
                pinBtn.Click += OnMessagePinClicked;
                controlStack.Children.Add(pinBtn);

                var suppressBtn = new Button { Content = "👁️", Classes = { "msgSuppressBtn" }, Padding = new Thickness(4, 1), FontSize = 9, BorderThickness = new Thickness(0) };
                suppressBtn.Tag = segment.Id;
                suppressBtn.Click += OnMessageSuppressClicked;
                controlStack.Children.Add(suppressBtn);

                Grid.SetColumn(segmentLabel, 0);
                Grid.SetColumn(controlStack, 1);
                segmentGrid.Children.Add(segmentLabel);
                segmentGrid.Children.Add(controlStack);
                segmentBorder.Child = segmentGrid;

                CompressedSegmentsContainer?.Children.Add(segmentBorder);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Error refreshing compressed segments: {Message}", ex.Message);
        }
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

            // Note: HeaderBudgetPercentText was not defined in XAML — budget display is handled by left sidebar text block
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Error refreshing context budget: {Message}", ex.Message);

            // Set fallback text on errors
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
    }

    private void OnRightAddCustomContextClicked(object? sender, RoutedEventArgs e)
    {
        // Toggle custom context injection panel visibility from right sidebar button
        if (CustomContextInjectionPanel != null)
            CustomContextInjectionPanel.IsVisible = !CustomContextInjectionPanel.IsVisible;
    }

    private async void OnRightCustomContextInjectClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextManager == null || _selectedChatId == null) return;

        // Get the injection type from the ComboBox selection (0 = System Prompt custom, 1 = File Contents, 2 = Raw Context)
        var selectedTypeIndex = ContextInjectionTypeSelector?.SelectedIndex ?? 0;

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
                CustomContextContentInput?.Text ?? "",
                injectionType);

            // Add visual representation to the left sidebar segments container
            if (segment != null && CustomContextSegmentsContainer != null)
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
                CustomContextSegmentsContainer.Children.Add(segmentBorder);
            }

            // Collapse the panel after injection
            if (CustomContextInjectionPanel != null)
                CustomContextInjectionPanel.IsVisible = false;
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

                // Remove the visual representation from the UI
                if (_customContextBorders.TryRemove(segmentIdObj.Value, out var borderToRemove))
                {
                    CustomContextSegmentsContainer.Children.Remove(borderToRemove);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to remove custom context");
            }
        }
    }
}