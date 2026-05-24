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
        if (FlagCountText != null)
            FlagCountText.Text = $"Flags: {MineCount - _flagsPlaced}";
    }

    private void UpdateTimerDisplay()
    {
        if (TimerDisplay != null)
            TimerDisplay.Text = $"⏱ {_seconds}s";
    }

    private void UpdateFaceEmoji(string emoji)
    {
        if (ResetButton != null)
            ResetButton.Text = emoji;
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

        var canvas = new Canvas { Width = Cols * CellSize, Height = Rows * CellSize };

        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var idx = r * Cols + c;
                var state = _states[idx];

                var fill = state switch
                {
                    0 => _mouseDown ? Brushes.Gray : Brushes.Silver,
                    1 => _hasMines[idx] ? Brushes.Red : Brushes.LightGray,
                    2 => Brushes.Silver,
                    _ => Brushes.LightGray,
                };

                // Cell background
                var rect = new Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    Fill = fill,
                    Stroke = _mouseDown && state == 0 ? Brushes.Gray : Brushes.Gray,
                    StrokeThickness = 1,
                    RadiusX = 1,
                    RadiusY = 1,
                };
                Canvas.SetLeft(rect, c * CellSize);
                Canvas.SetTop(rect, r * CellSize);
                canvas.Children.Add(rect);

                // Text overlay (flag, mine, or number) — wrap in StackPanel to constrain size for proper centering
                if (state == 2)
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
                else if (state == 1 && _hasMines[idx])
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
                else if (state == 1 && _adjacentMines[idx] > 0)
                {
                    var colors = new[] { Brushes.Blue, Brushes.Green, Brushes.Red, Brushes.DarkBlue, Brushes.DarkRed, Brushes.Teal, Brushes.Black, Brushes.Gray };
                    var numText = new TextBlock
                    {
                        Text = _adjacentMines[idx].ToString(),
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
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