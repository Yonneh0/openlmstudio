// Git log table control — displays parsed GitLogEntry items in a styled table

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// A control that displays git log entries in an elegant table with alternating row backgrounds,
/// hover effects, and proper column sizing.
/// </summary>
public class GitLogTable : ContentControl
{
    public static readonly StyledProperty<IEnumerable<GitLogEntry>?> EntriesProperty =
        AvaloniaProperty.Register<GitLogTable, IEnumerable<GitLogEntry>?>(nameof(Entries));

    public IEnumerable<GitLogEntry>? Entries
    {
        get => GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    private static readonly IBrush _headerBackground = new SolidColorBrush(Color.Parse("#2D2D30"));
    private static readonly IBrush _hoverBackground = new SolidColorBrush(Color.Parse("#3A3A3E"));
    private static readonly IBrush _borderBrush = new SolidColorBrush(Color.Parse("#333338"));

    private readonly ScrollViewer _scrollViewer = new();
    private Grid _tableGrid = new();

    public GitLogTable()
    {
        Foreground = Brushes.White;
        FontSize = 12.0;

        // Build the visual tree
        _scrollViewer.Content = _tableGrid;
        _scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

        SetValue(Avalonia.Controls.ContentControl.ContentProperty, _scrollViewer);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntriesProperty)
            RebuildRows();
    }

    private void RebuildRows()
    {
        var entries = Entries?.ToArray() ?? Array.Empty<GitLogEntry>();
        var rowCount = entries.Length;

        // Layout constants
        const double headerHeight = 28;
        const double rowHeight = 26;
        const double rowPadding = 8;
        const double hashColumnWidth = 120;
        const double authorColumnWidth = 180;
        const double messageColumnWidth = 400;
        const double borderWidth = 1;

        // Total width
        var totalWidth = hashColumnWidth + authorColumnWidth + messageColumnWidth + rowPadding * 3 + borderWidth * 2;

        // Clear existing rows
        _tableGrid.Children.Clear();

        // Set column definitions
        _tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        // Draw header row
        var headerRow = new Grid
        {
            Height = headerHeight,
            Background = _headerBackground,
        };

        headerRow.Children.Add(CreateHeaderCell("COMMIT", 11, rowPadding, 0));
        Grid.SetColumn(headerRow.Children[0], 0);

        headerRow.Children.Add(CreateHeaderCell("AUTHOR", 11, rowPadding, 1));
        Grid.SetColumn(headerRow.Children[1], 1);

        headerRow.Children.Add(CreateHeaderCell("MESSAGE", 11, rowPadding, 2));
        Grid.SetColumn(headerRow.Children[2], 2);

        // Header border
        var headerBorder = new Border
        {
            Background = _headerBackground,
            Child = headerRow,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = _borderBrush,
        };
        Grid.SetRow(headerBorder, 0);
        _tableGrid.Children.Add(headerBorder);

        // Draw rows
        for (var i = 0; i < rowCount; i++)
        {
            var entry = entries[i];
            var bgBrush = i % 2 == 0
                ? new SolidColorBrush(Color.Parse("#252529"))
                : _headerBackground;

            var rowGrid = new Grid
            {
                Width = totalWidth,
                Height = rowHeight,
                Background = bgBrush,
            };
            Grid.SetRow(rowGrid, 1);

            // Add hover effect
            rowGrid.AddHandler(PointerEnteredEvent, OnRowPointerEntered);
            rowGrid.AddHandler(PointerExitedEvent, OnRowPointerExited);
            rowGrid.AddHandler(PointerPressedEvent, OnRowPointerPressed);

            // Commit hash (monospace, truncated)
            var shortHash = entry.Hash.Length > 8 ? entry.Hash.Substring(0, 8) : entry.Hash;
            var hashBrush = new SolidColorBrush(Color.Parse("#666666"));
            rowGrid.Children.Add(CreateCell(shortHash, 11, rowPadding, hashBrush, 0));
            Grid.SetColumn(rowGrid.Children[0], 0);

            // Author (truncated)
            var author = entry.Author.Length > 30 ? entry.Author.Substring(0, 27) + "..." : entry.Author;
            var authorBrush = new SolidColorBrush(Color.Parse("#888888"));
            rowGrid.Children.Add(CreateCell(author, 11, rowPadding, authorBrush, 1));
            Grid.SetColumn(rowGrid.Children[1], 1);

            // Message
            var message = entry.Message.Length > 60 ? entry.Message.Substring(0, 57) + "..." : entry.Message;
            var messageBrush = new SolidColorBrush(Color.Parse("#CCCCCC"));
            rowGrid.Children.Add(CreateCell(message, 11, rowPadding, messageBrush, 2));
            Grid.SetColumn(rowGrid.Children[2], 2);

            _tableGrid.Children.Add(rowGrid);
        }
    }

    private static TextBlock CreateHeaderCell(string text, double fontSize, double padding, int column)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = fontSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = _borderBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Padding = new Thickness(padding, 0, 0, 0),
        };
        Grid.SetColumn(tb, column);
        return tb;
    }

    private static TextBlock CreateCell(string text, double fontSize, double padding, IBrush brush, int column)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = fontSize,
            Foreground = brush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Padding = new Thickness(padding, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(tb, column);
        return tb;
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Grid row) return;
        row.Background = _hoverBackground;
        e.Handled = true;
    }

    private void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Grid row) return;
        var index = Grid.GetRow(row) - 1;
        if (index >= 0)
        {
            row.Background = index % 2 == 0
                ? new SolidColorBrush(Color.Parse("#252529"))
                : _headerBackground;
            e.Handled = true;
        }
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Grid row) return;
        e.Handled = true;
    }
}