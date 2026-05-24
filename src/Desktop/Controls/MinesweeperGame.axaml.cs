using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// A seven-segment LED display control that renders digits as red segments on a dark background.
/// Uses a Canvas of Path elements with polygon shapes for the characteristic LED segment shape.
/// </summary>
public class SevenSegmentDisplay : Panel
{
    private const int Digits = 3;
    private const int DigitWidth = 18;
    private const int DigitHeight = 36;
    private const int Gap = 6;
    private const int SegThickness = 4;

    private const int TotalWidth = Digits * (DigitWidth + Gap) - Gap;
    private const int TotalHeight = DigitHeight;

    private Canvas? _canvas;
    private Avalonia.Controls.Shapes.Rectangle[]? _segments;
    private int _digit;

    // Classic Minesweeper LED red (bright, vivid red for active segments)
    private static readonly SolidColorBrush _segmentBrush = new(Color.FromArgb(255, 255, 20, 20));
    // Dark background for segments that are off (ghost segments)
    private static readonly SolidColorBrush _bgBrush = new(Color.FromArgb(255, 0, 0, 0));
    // Ghost segment color (bright red, clearly visible as ghost segments)
    private static readonly SolidColorBrush _ghostBrush = new(Color.FromArgb(255, 255, 220, 220));

    // 7 segments per digit, numbered:
    //  000
    // 1   2
    //  333
    // 4   5
    //  666
    // Horizontal (0,3,6): top/bottom/middle segments
    // Vertical (1,2,4,5): left/right segments
    // Each segment is a compound polygon with angled ends (characteristic LED shape)
    private static readonly int[][] _digitPatterns =
    [
        [0, 1, 2, 3, 4, 5, 6], // 0
        [2, 5],                  // 1
        [0, 2, 3, 4, 6],       // 2
        [0, 2, 3, 5, 6],       // 3
        [1, 2, 5, 6],          // 4
        [0, 1, 3, 5, 6],       // 5
        [0, 1, 3, 4, 5, 6],    // 6
        [0, 2, 5],              // 7
        [0, 1, 2, 3, 4, 5, 6], // 8
        [0, 1, 2, 3, 5, 6],    // 9
    ];

    // Pre-computed segment geometries for horizontal and vertical segments
    // Horizontal: trapezoid wider at bottom (DigitWidth x SegThickness)
    // Vertical: trapezoid wider at bottom (SegThickness x VerticalSegHeight)
    // KEY: Path bounding box aspect ratio MUST match the Rectangle it's placed in,
    //      otherwise Stretch.Fill distorts the trapezoid shape.
    private static readonly Geometry _hSegGeo;
    private static readonly Geometry _vSegGeo;

    // Vertical segment spacing (24px tall, 2px gap between upper/lower halves)
    private const int VerticalSegHeight = 24;

    static SevenSegmentDisplay()
    {
        var t = SegThickness;
        var hw = t / 3; // adjusted so Path bounding box matches Rectangle aspect ratio
        var dW = DigitWidth;

        // Horizontal segment (angled cuts on left and right):
        // Bounding box: dW x t = 18 x 4 (aspect ratio 4.5)
        // Top width: dW - 2*(dW - t) = 18 - 12 = 6
        // Bottom width: dW = 18
        // Ratio: 18/6 = 3.0, matches hw=4/3 trapezoid ratio
        var hPts = new Point[]
        {
            new(0, t/2),
            new(0, 0),
            new(t/2, hw),
            new(dW - t/2, hw),
            new(dW, 0),
            new(dW, t),
            new(dW - t/2, t - hw),
            new(t/2, t - hw),
        };
        _hSegGeo = Geometry.Parse("M" + string.Join(" L", hPts) + " Z");

        // Vertical segment (angled cuts on top and bottom):
        // Bounding box: t x VerticalSegHeight = 6 x 24 (aspect ratio 4.0)
        // Top width: 2*hw = 8/3 ≈ 2.67
        // Bottom width: t = 4
        // Ratio: 4 / (8/3) = 3.0
        // -> Wider at bottom, classic LED look (same as horizontal)
        var vPts = new Point[]
        {
            new(t/2, 0),
            new(t, t/2),
            new(t - hw, t/2),
            new(t - hw, VerticalSegHeight - t/2),
            new(t - hw, VerticalSegHeight),
            new(hw, VerticalSegHeight),
            new(hw, t/2),
            new(t/2, t/2),
        };
        _vSegGeo = Geometry.Parse("M" + string.Join(" L", vPts) + " Z");
    }

    private void UpdateSegments()
    {
        if (_canvas == null || _segments == null) return;

        var value = Digit;
        var digits = new int[Digits];
        for (int i = Digits - 1; i >= 0; i--)
        {
            digits[i] = value % 10;
            value /= 10;
        }

        var segIndex = 0;
        for (int digitIdx = 0; digitIdx < Digits; digitIdx++)
        {
            var digit = digits[digitIdx];
            var pattern = _digitPatterns[digit];
            var offsetX = digitIdx * (DigitWidth + Gap);

            for (int segIdx = 0; segIdx < 7; segIdx++)
            {
                var isActive = pattern.Contains(segIdx);
                var rect = _segments[segIndex];

                // 7 segments per digit, numbered:
                //  000
                // 1   2
                //  333
                // 4   5
                //  666
                // Horizontal (0,3,6): full width (DigitWidth), SegThickness tall
                // Vertical (1,2,4,5): SegThickness wide, VerticalSegHeight tall
                if (segIdx == 0)
                {
                    // Top horizontal
                    rect.Width = DigitWidth;
                    rect.Height = SegThickness;
                    Canvas.SetLeft(rect, offsetX);
                    Canvas.SetTop(rect, 0);
                }
                else if (segIdx == 1)
                {
                    // Upper-left vertical (wider at bottom)
                    rect.Width = SegThickness;
                    rect.Height = VerticalSegHeight;
                    Canvas.SetLeft(rect, offsetX);
                    Canvas.SetTop(rect, 0);
                }
                else if (segIdx == 2)
                {
                    // Upper-right vertical (wider at bottom)
                    rect.Width = SegThickness;
                    rect.Height = VerticalSegHeight;
                    Canvas.SetLeft(rect, offsetX + DigitWidth - SegThickness);
                    Canvas.SetTop(rect, 0);
                }
                else if (segIdx == 3)
                {
                    // Middle horizontal (wider at bottom)
                    rect.Width = DigitWidth;
                    rect.Height = SegThickness;
                    Canvas.SetLeft(rect, offsetX);
                    Canvas.SetTop(rect, (DigitHeight - SegThickness) / 2);
                }
                else if (segIdx == 4)
                {
                    // Lower-left vertical (wider at bottom)
                    rect.Width = SegThickness;
                    rect.Height = VerticalSegHeight;
                    Canvas.SetLeft(rect, offsetX);
                    Canvas.SetTop(rect, (DigitHeight - SegThickness) / 2 + SegThickness + 2);
                }
                else if (segIdx == 5)
                {
                    // Lower-right vertical (wider at bottom)
                    rect.Width = SegThickness;
                    rect.Height = VerticalSegHeight;
                    Canvas.SetLeft(rect, offsetX + DigitWidth - SegThickness);
                    Canvas.SetTop(rect, (DigitHeight - SegThickness) / 2 + SegThickness + 2);
                }
                else // segIdx == 6
                {
                    // Bottom horizontal (wider at bottom)
                    rect.Width = DigitWidth;
                    rect.Height = SegThickness;
                    Canvas.SetLeft(rect, offsetX);
                    Canvas.SetTop(rect, DigitHeight - SegThickness);
                }

                rect.Fill = isActive ? _segmentBrush : _ghostBrush;
                segIndex++;
            }
        }
    }

    public int Digit
    {
        get => _digit;
        set
        {
            _digit = value;
            UpdateSegments();
        }
    }

    public static readonly StyledProperty<int> DigitProperty =
        AvaloniaProperty.Register<SevenSegmentDisplay, int>(nameof(Digit), 0);

    public SevenSegmentDisplay()
    {
        Width = TotalWidth;
        Height = TotalHeight;

        _canvas = new Canvas
        {
            Width = TotalWidth,
            Height = TotalHeight
        };

        // Background
        _canvas.Children.Add(new Rectangle
        {
            Width = TotalWidth,
            Height = TotalHeight,
            Fill = _bgBrush
        });

        // Create 21 segment rectangles (7 per digit x 3 digits)
        _segments = new Rectangle[21];
        for (int i = 0; i < 21; i++)
        {
            var rect = new Rectangle
            {
                Fill = _bgBrush,
                RadiusX = 1,
                RadiusY = 1,
            };
            _segments[i] = rect;
            _canvas.Children.Add(rect);
        }

        this.Children.Add(_canvas);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs changed)
    {
        base.OnPropertyChanged(changed);
        if (changed.Property == DigitProperty)
            UpdateSegments();
    }
}

public partial class MinesweeperGame : UserControl
{
    private const int Rows = 10;
    private const int Cols = 10;
    private const int MineCount = 10;
    private const int CellSize = 30;

    private bool[] _hasMines = [];
    private int[] _adjacentMines = [];
    private int[] _states = []; // 0=Hidden, 1=Revealed, 2=Flagged
    private bool _gameOver;
    private bool _gameStarted;
    private bool _mouseDown;
    private int _flagsPlaced;
    private int _seconds;
    private DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private static readonly string _bestTimesFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenLMStudio",
        "minesweeper-best-times.json");

    private record BestTimeEntry(int Seconds);

    public MinesweeperGame()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        ResetButton.PointerPressed += OnResetClicked;
        GameBoard.AddHandler(PointerPressedEvent, OnBoardPointerPressed, handledEventsToo: true);
        GameBoard.AddHandler(PointerReleasedEvent, OnBoardPointerReleased, handledEventsToo: true);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        _gameOver = false;
        _gameStarted = false;
        _mouseDown = false;
        _flagsPlaced = 0;
        _seconds = 0;
        _timer.Stop();

        UpdateBestTimes();

        var count = Rows * Cols;
        _hasMines = new bool[count];
        _adjacentMines = new int[count];
        _states = new int[count];

        var random = new Random();
        var placed = 0;
        while (placed < MineCount)
        {
            var idx = random.Next(count);
            if (!_hasMines[idx])
            {
                _hasMines[idx] = true;
                placed++;
            }
        }

        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var idx = r * Cols + c;
                if (_hasMines[idx]) continue;
                _adjacentMines[idx] = CountAdjacentMines(r, c);
            }
        }

        UpdateFlagCount();
        UpdateTimerDisplay();
        UpdateFaceEmoji("🙂");
        RenderBoard();
        if (StatusText != null)
            StatusText.Text = "Click to start";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_gameOver) { _timer.Stop(); return; }
        _seconds++;
        UpdateTimerDisplay();
    }

    private int CountAdjacentMines(int row, int col)
    {
        var count = 0;
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                var nr = row + dr;
                var nc = col + dc;
                if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols && _hasMines[nr * Cols + nc])
                    count++;
            }
        }
        return count;
    }

    private void SafeCellSwap(int clickedRow, int clickedCol)
    {
        var clickedIdx = clickedRow * Cols + clickedCol;
        _hasMines[clickedIdx] = false;
        _adjacentMines[clickedIdx] = CountAdjacentMines(clickedRow, clickedCol);

        // Try adjacent cells first, then any safe cell in the grid
        var adjacentCells = new List<(int R, int C)>();
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                var nr = clickedRow + dr;
                var nc = clickedCol + dc;
                if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols)
                    adjacentCells.Add((nr, nc));
            }
        }

        var random = new Random();
        adjacentCells = adjacentCells.OrderBy(_ => random.Next()).ToList();

        foreach (var (fr, fc) in adjacentCells)
        {
            var fIdx = fr * Cols + fc;
            if (!_hasMines[fIdx])
            {
                _hasMines[fIdx] = true;
                _adjacentMines[fIdx] = CountAdjacentMines(fr, fc);
                return;
            }
        }

        // All adjacent cells are mines — find any safe cell in the grid
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var idx = r * Cols + c;
                if (idx != clickedIdx && !_hasMines[idx])
                {
                    _hasMines[idx] = true;
                    _adjacentMines[idx] = CountAdjacentMines(r, c);
                    return;
                }
            }
        }
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        InitializeGame();
    }

    private void OnBoardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _mouseDown = true;
        UpdateFaceEmoji("😟");

        if (_gameOver) return;

        var pos = e.GetPosition(GameBoard);
        var col = (int)(pos.X / CellSize);
        var row = (int)(pos.Y / CellSize);

        col = Math.Max(0, Math.Min(Cols - 1, col));
        row = Math.Max(0, Math.Min(Rows - 1, row));

        if (row < 0 || row >= Rows || col < 0 || col >= Cols) return;

        var props = e.GetCurrentPoint(GameBoard).Properties;
        if (props.IsRightButtonPressed)
        {
            var idx = row * Cols + col;
            if (_states[idx] == 0)
            {
                _states[idx] = 2;
                _flagsPlaced++;
            }
            else if (_states[idx] == 2)
            {
                _states[idx] = 0;
                _flagsPlaced--;
            }
            UpdateFlagCount();
            RenderBoard();
            return;
        }

        var idx2 = row * Cols + col;
        if (_states[idx2] != 0) return;

        if (!_gameStarted)
        {
            _gameStarted = true;
            _timer.Start();
            if (StatusText != null) StatusText.Text = "Playing...";

            // First click should never be a mine — swap mine to a safe adjacent cell
            if (_hasMines[idx2])
            {
                SafeCellSwap(row, col);
            }
        }
        else if (_hasMines[idx2])
        {
            _gameOver = true;
            UpdateFaceEmoji("💀");
            if (StatusText != null) StatusText.Text = "Game Over!";
            _timer.Stop();
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    if (_hasMines[r * Cols + c])
                        _states[r * Cols + c] = 1;
                }
            }
            RenderBoard();
            return;
        }

        RevealCell(row, col);
        CheckWin();
        RenderBoard();
    }

    private void OnBoardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _mouseDown = false;
        if (!_gameOver && _gameStarted)
            UpdateFaceEmoji("🙂");
        RenderBoard();
    }

    private void RevealCell(int row, int col)
    {
        if (row < 0 || row >= Rows || col < 0 || col >= Cols) return;
        var idx = row * Cols + col;
        if (_states[idx] != 0) return;
        if (_hasMines[idx]) return;

        _states[idx] = 1;

        if (_adjacentMines[idx] == 0)
        {
            for (var dr = -1; dr <= 1; dr++)
            {
                for (var dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    RevealCell(row + dr, col + dc);
                }
            }
        }
    }

    private void CheckWin()
    {
        var hiddenNonMines = 0;
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var idx = r * Cols + c;
                if (_states[idx] == 0 && !_hasMines[idx])
                    hiddenNonMines++;
            }
        }

        if (hiddenNonMines == 0)
        {
            _gameOver = true;
            _timer.Stop();
            UpdateFaceEmoji("😎");
            if (StatusText != null) StatusText.Text = "You Win!";

            SaveBestTime(_seconds);
            UpdateBestTimes();
        }
    }

    private void UpdateFlagCount()
    {
        if (FlagCountSegment != null)
            FlagCountSegment.Digit = MineCount - _flagsPlaced;
    }

    private void UpdateTimerDisplay()
    {
        if (TimerSegment != null)
            TimerSegment.Digit = Math.Min(_seconds, 999);
    }

    private void UpdateFaceEmoji(string emoji)
    {
        if (ResetEmoji != null)
            ResetEmoji.Text = emoji;
    }

    private void UpdateBestTimes()
    {
        if (BestTimesText == null) return;
        try
        {
            var entries = LoadBestTimes();
            if (entries.Count == 0)
                BestTimesText.Text = "Best times:";
            else
                BestTimesText.Text = "Best: " + string.Join(", ", entries.Take(5).Select(e => e.Seconds + "s"));
        }
        catch
        {
            BestTimesText.Text = "Best times:";
        }
    }

    private static void SaveBestTime(int seconds)
    {
        try
        {
            var entries = LoadBestTimes();
            entries.Add(new BestTimeEntry(seconds));
            entries.Sort((a, b) => a.Seconds - b.Seconds);
            entries = entries.Take(10).ToList();

            var dir = System.IO.Path.GetDirectoryName(_bestTimesFile);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            using var sw = new StreamWriter(_bestTimesFile);
            sw.Write(string.Join(",", entries.Select(e => e.Seconds)));
        }
        catch { }
    }

    private static List<BestTimeEntry> LoadBestTimes()
    {
        try
        {
            if (!File.Exists(_bestTimesFile)) return new List<BestTimeEntry>();
            var content = File.ReadAllText(_bestTimesFile);
            if (string.IsNullOrWhiteSpace(content)) return new List<BestTimeEntry>();
            return content.Split(',').Select(int.Parse).Select(s => new BestTimeEntry(s)).ToList();
        }
        catch { return new List<BestTimeEntry>(); }
    }

    private void RenderBoard()
    {
        if (GameBoard != null)
            GameBoard.Child = null;

        var canvas = new Canvas { Width = Cols * CellSize + 2, Height = Rows * CellSize + 2 };

        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var idx = r * Cols + c;
                var state = _states[idx];

                // Classic Windows Minesweeper cell colors
                // Raised cell (hidden): light gray with bevel
                // Sunken cell (revealed): dark gray
                // Flagged: raised with flag
                // Left border (dark): #808080
                // Top border (dark): #808080
                // Right border (light): #FFFFFF
                // Bottom border (light): #FFFFFF

                var isRevealed = state == 1;
                var isFlagged = state == 2;

                // Cell fill colors
        Brush cellFill;
        Brush leftBorder;
        Brush topBorder;
        Brush rightBorder;
        Brush bottomBorder;

        if (isRevealed)
        {
            // Sunken (revealed) cell
            cellFill = _hasMines[idx] ? new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)) : new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            leftBorder = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            topBorder = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            rightBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            bottomBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
        else if (isFlagged)
        {
            // Flagged cell (raised)
            cellFill = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            leftBorder = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            topBorder = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            rightBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            bottomBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }
        else
        {
            // Hidden cell (raised)
            cellFill = new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            leftBorder = _mouseDown ? new SolidColorBrush(Color.FromArgb(255, 128, 128, 128)) : new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            topBorder = _mouseDown ? new SolidColorBrush(Color.FromArgb(255, 128, 128, 128)) : new SolidColorBrush(Color.FromArgb(255, 192, 192, 192));
            rightBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            bottomBorder = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

                // Cell background
                var rect = new Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    Fill = cellFill,
                };
                Canvas.SetLeft(rect, c * CellSize);
                Canvas.SetTop(rect, r * CellSize);
                canvas.Children.Add(rect);

                // 3D bevel borders
                // Left border (dark)
                var leftBorderRect = new Rectangle
                {
                    Width = 1,
                    Height = CellSize,
                    Fill = leftBorder,
                };
                Canvas.SetLeft(leftBorderRect, c * CellSize);
                Canvas.SetTop(leftBorderRect, r * CellSize);
                canvas.Children.Add(leftBorderRect);

                // Top border (dark)
                var topBorderRect = new Rectangle
                {
                    Width = CellSize,
                    Height = 1,
                    Fill = topBorder,
                };
                Canvas.SetLeft(topBorderRect, c * CellSize);
                Canvas.SetTop(topBorderRect, r * CellSize);
                canvas.Children.Add(topBorderRect);

                // Right border (light)
                var rightBorderRect = new Rectangle
                {
                    Width = 1,
                    Height = CellSize,
                    Fill = rightBorder,
                };
                Canvas.SetLeft(rightBorderRect, (c + 1) * CellSize - 1);
                Canvas.SetTop(rightBorderRect, r * CellSize);
                canvas.Children.Add(rightBorderRect);

                // Bottom border (light)
                var bottomBorderRect = new Rectangle
                {
                    Width = CellSize,
                    Height = 1,
                    Fill = bottomBorder,
                };
                Canvas.SetLeft(bottomBorderRect, c * CellSize);
                Canvas.SetTop(bottomBorderRect, (r + 1) * CellSize - 1);
                canvas.Children.Add(bottomBorderRect);

                // Text overlay (flag, mine, or number)
                if (isFlagged)
                {
                    var flagText = new TextBlock
                    {
                        Text = "\u2694",
                        FontSize = 16,
                        Foreground = Brushes.Red,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var panel = new StackPanel { Width = CellSize, Height = CellSize, Children = { flagText } };
                    Canvas.SetLeft(panel, c * CellSize);
                    Canvas.SetTop(panel, r * CellSize);
                    canvas.Children.Add(panel);
                }
                else if (isRevealed && _hasMines[idx])
                {
                    var mineText = new TextBlock
                    {
                        Text = "\u2620",
                        FontSize = 16,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var panel = new StackPanel { Width = CellSize, Height = CellSize, Children = { mineText } };
                    Canvas.SetLeft(panel, c * CellSize);
                    Canvas.SetTop(panel, r * CellSize);
                    canvas.Children.Add(panel);
                }
                else if (isRevealed && _adjacentMines[idx] > 0)
                {
                    // Classic Windows Minesweeper colors: 1=blue, 2=green, 3=red, 4=dark blue, 5=dark red, 6=teal, 7=black, 8=gray
                    var colors = new[]
                    {
                        Brushes.Blue,
                        Brushes.Green,
                        Brushes.Red,
                        Brushes.DarkBlue,
                        Brushes.DarkRed,
                        Brushes.Teal,
                        Brushes.Black,
                        Brushes.Gray,
                    };
                    var numText = new TextBlock
                    {
                        Text = _adjacentMines[idx].ToString(),
                    FontSize = 16,
                    FontWeight = FontWeight.Normal,
                    Foreground = colors[_adjacentMines[idx] - 1],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var panel = new StackPanel { Width = CellSize, Height = CellSize, Children = { numText } };
                    Canvas.SetLeft(panel, c * CellSize);
                    Canvas.SetTop(panel, r * CellSize);
                    canvas.Children.Add(panel);
                }
            }
        }

        if (GameBoard != null)
            GameBoard.Child = canvas;
    }
}
