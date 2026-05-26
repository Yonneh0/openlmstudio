// Git log table control — displays parsed GitLogEntry items in an elegant table

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Platform;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// A control that displays git log entries in an elegant table with alternating row backgrounds,
/// hover effects, column separators, and proper row stacking.
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

    // Colors
    private static readonly IBrush _bgPrimary = new SolidColorBrush(Color.Parse("#1E1E22"));
    private static readonly IBrush _bgSecondary = new SolidColorBrush(Color.Parse("#252529"));
    private static readonly IBrush _bgHeader = new SolidColorBrush(Color.Parse("#2D2D30"));
    private static readonly IBrush _bgHover = new SolidColorBrush(Color.Parse("#3A3A3E"));
    private static readonly IBrush _bgSelected = new SolidColorBrush(Color.Parse("#1E3A5F"));
    private static readonly IBrush _borderBrush = new SolidColorBrush(Color.Parse("#3A3A3E"));
    private static readonly IBrush _headerText = new SolidColorBrush(Color.Parse("#999999"));
    private static readonly IBrush _hashColor = new SolidColorBrush(Color.Parse("#888888"));
    private static readonly IBrush _authorColor = new SolidColorBrush(Color.Parse("#AAAAAA"));
    private static readonly IBrush _messageColor = new SolidColorBrush(Color.Parse("#E0E0E0"));
    private static readonly IBrush _selectionColor = new SolidColorBrush(Color.Parse("#1E88E5"));

    // Layout constants
    private const double HeaderHeight = 32;
    private const double RowHeight = 30;
    private const double RowPadding = 12;
    private const double HashColumnWidth = 130;
    private const double AuthorColumnWidth = 200;
    private const double SeparatorWidth = 1;
    private const double TopPadding = 8;

    // Visual tree
    private readonly ScrollViewer _scrollViewer = new();
    private readonly StackPanel _rowsPanel = new();
    private readonly Border _outerBorder = new();
    private readonly Border _headerBorder = new();
    private readonly Grid _headerGrid = new();

    // State
    private int? _selectedIndex;
    private GitLogEntry[] _allEntries = Array.Empty<GitLogEntry>();

    public GitLogTable()
    {
        Foreground = _messageColor;
        FontSize = 12.0;
        Background = _bgPrimary;

        // Build the visual tree
        BuildVisualTree();
    }

    private void BuildVisualTree()
    {
        // Outer border with corner radius
        _outerBorder.Background = _bgPrimary;
        _outerBorder.BorderBrush = _borderBrush;
        _outerBorder.BorderThickness = new Thickness(1);
        _outerBorder.CornerRadius = new CornerRadius(6);
        _outerBorder.Padding = new Thickness(0, TopPadding, 0, 0);

        // Header row
        _headerGrid.Height = HeaderHeight;
        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HashColumnWidth) });
        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeparatorWidth) });
        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AuthorColumnWidth) });
        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeparatorWidth) });
        _headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        // Header cells
        var hashHeader = CreateHeaderCell("COMMIT", _hashColor);
        Grid.SetColumn(hashHeader, 0);
        _headerGrid.Children.Add(hashHeader);

        var hashSep = CreateSeparator();
        Grid.SetColumn(hashSep, 1);
        _headerGrid.Children.Add(hashSep);

        var authorHeader = CreateHeaderCell("AUTHOR", _headerText);
        Grid.SetColumn(authorHeader, 2);
        _headerGrid.Children.Add(authorHeader);

        var authorSep = CreateSeparator();
        Grid.SetColumn(authorSep, 3);
        _headerGrid.Children.Add(authorSep);

        var messageHeader = CreateHeaderCell("MESSAGE", _headerText);
        Grid.SetColumn(messageHeader, 4);
        _headerGrid.Children.Add(messageHeader);

        _headerBorder.Child = _headerGrid;
        _headerBorder.Background = _bgHeader;
        _headerBorder.BorderThickness = new Thickness(0, 0, 0, 1);
        _headerBorder.BorderBrush = _borderBrush;

        // Rows panel
        _rowsPanel.Children.Clear();
        _rowsPanel.Orientation = Orientation.Vertical;

        // Scroll viewer: header sits above, rows scroll below
        _scrollViewer.Content = new StackPanel
        {
            Children = { _headerBorder, _rowsPanel },
        };
        _scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        _outerBorder.Child = _scrollViewer;
        SetValue(ContentControl.ContentProperty, _outerBorder);
    }

    private static TextBlock CreateHeaderCell(string text, IBrush foreground)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(RowPadding, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Width = SeparatorWidth,
            Background = _borderBrush,
            Margin = new Thickness(0, 6, 0, 6),
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntriesProperty)
            RebuildRows();
    }

    private void RebuildRows()
    {
        _allEntries = (Entries as GitLogEntry[])?.ToArray() ?? (Entries?.ToArray() ?? Array.Empty<GitLogEntry>());
        _rowsPanel.Children.Clear();

        if (_allEntries.Length == 0)
        {
            var emptyRow = new TextBlock
            {
                Text = "No commits",
                Foreground = _headerText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(20),
            };
            _rowsPanel.Children.Add(new Border { Child = emptyRow, Background = _bgPrimary });
            return;
        }

        for (var i = 0; i < _allEntries.Length; i++)
        {
            var entry = _allEntries[i];
            var row = CreateRow(entry, i);
            _rowsPanel.Children.Add(row);
        }
    }

    private Border CreateRow(GitLogEntry entry, int index)
    {
        var border = new Border
        {
            Height = RowHeight,
            Background = index % 2 == 0 ? _bgSecondary : _bgPrimary,
            Margin = new Thickness(0, 0, 0, 1),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        var grid = new Grid
        {
            Height = RowHeight,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HashColumnWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeparatorWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AuthorColumnWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SeparatorWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        // Commit hash
        var shortHash = entry.Hash.Length > 10 ? entry.Hash.Substring(0, 10) : entry.Hash;
        var hashText = new TextBlock
        {
            Text = shortHash,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = _hashColor,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(RowPadding, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(hashText, 0);
        grid.Children.Add(hashText);

        // Separator
        var sep1 = CreateSeparator();
        Grid.SetColumn(sep1, 1);
        grid.Children.Add(sep1);

        // Author
        var author = entry.Author.Length > 25 ? entry.Author.Substring(0, 22) + "..." : entry.Author;
        var authorText = new TextBlock
        {
            Text = author,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = _authorColor,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(RowPadding, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(authorText, 2);
        grid.Children.Add(authorText);

        // Separator
        var sep2 = CreateSeparator();
        Grid.SetColumn(sep2, 3);
        grid.Children.Add(sep2);

        // Message
        var message = entry.Message.Length > 55 ? entry.Message.Substring(0, 52) + "..." : entry.Message;
        var messageText = new TextBlock
        {
            Text = message,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = _messageColor,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(RowPadding, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(messageText, 4);
        grid.Children.Add(messageText);

        border.Child = grid;

        // Events
        border.AddHandler(PointerEnteredEvent, OnRowPointerEntered);
        border.AddHandler(PointerExitedEvent, OnRowPointerExited);
        border.AddHandler(PointerPressedEvent, OnRowPointerPressed);

        return border;
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border) return;
        if (_selectedIndex == null)
            border.Background = _bgHover;
        e.Handled = true;
    }

    private void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border) return;
        if (_selectedIndex == null)
        {
            var index = _rowsPanel.Children.IndexOf(border);
            border.Background = index % 2 == 0 ? _bgSecondary : _bgPrimary;
        }
        e.Handled = true;
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border clickedBorder) return;
        var index = _rowsPanel.Children.IndexOf(clickedBorder);

        // Toggle selection
        if (_selectedIndex == index)
            _selectedIndex = null;
        else
            _selectedIndex = index;

        // Update all row backgrounds
        for (var i = 0; i < _rowsPanel.Children.Count; i++)
        {
            if (_rowsPanel.Children[i] is Border row)
            {
                row.Background = i == index
                    ? _bgSelected
                    : i % 2 == 0 ? _bgSecondary : _bgPrimary;
            }
        }

        // Copy commit hash to clipboard
        if (index >= 0 && index < _allEntries.Length)
        {
            var hash = _allEntries[index].Hash;
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var clipboard = lifetime?.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(hash));
                clipboard.SetDataAsync(data);
            }
        }

        e.Handled = true;
    }
}