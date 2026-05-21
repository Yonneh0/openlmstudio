using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

public partial class TetrisGame : UserControl
{
    private const int BoardWidth = 10;
    private const int BoardHeight = 20;
    private const int CellSize = 24;

    private enum PieceType { I, O, T, S, Z, J, L }
    private record Position(int Row, int Col);

    private static readonly Position[][][] Shapes =
    {
        // I
        new[] { new[] { new Position(0,1), new Position(1,1), new Position(2,1), new Position(3,1) },
                new[] { new Position(1,0), new Position(1,1), new Position(1,2), new Position(1,3) },
                new[] { new Position(0,2), new Position(1,2), new Position(2,2), new Position(3,2) },
                new[] { new Position(2,0), new Position(2,1), new Position(2,2), new Position(2,3) } },
        // O
        new[] { new[] { new Position(0,0), new Position(0,1), new Position(1,0), new Position(1,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,0), new Position(1,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,0), new Position(1,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,0), new Position(1,1) } },
        // T
        new[] { new[] { new Position(0,1), new Position(1,0), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,1), new Position(1,1), new Position(2,1), new Position(1,0) },
                new[] { new Position(0,1), new Position(1,0), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,1), new Position(1,1), new Position(2,1), new Position(1,2) } },
        // S
        new[] { new[] { new Position(0,1), new Position(0,2), new Position(1,0), new Position(1,1) },
                new[] { new Position(0,1), new Position(1,1), new Position(1,2), new Position(2,2) },
                new[] { new Position(0,1), new Position(0,2), new Position(1,0), new Position(1,1) },
                new[] { new Position(0,0), new Position(1,0), new Position(1,1), new Position(2,1) } },
        // Z
        new[] { new[] { new Position(0,0), new Position(0,1), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,2), new Position(1,1), new Position(1,2), new Position(2,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,1), new Position(1,0), new Position(1,1), new Position(2,0) } },
        // J
        new[] { new[] { new Position(0,0), new Position(1,0), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,1), new Position(0,2), new Position(1,1), new Position(2,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,0), new Position(2,0) },
                new[] { new Position(0,1), new Position(1,1), new Position(2,1), new Position(2,2) } },
        // L
        new[] { new[] { new Position(0,2), new Position(1,0), new Position(1,1), new Position(1,2) },
                new[] { new Position(0,1), new Position(1,1), new Position(2,1), new Position(2,2) },
                new[] { new Position(0,0), new Position(1,0), new Position(2,0), new Position(2,1) },
                new[] { new Position(0,0), new Position(0,1), new Position(1,1), new Position(2,1) } },
    };

    private static readonly IBrush[] PieceColors =
    {
        Brushes.Cyan, Brushes.Yellow, Brushes.Purple, Brushes.Green,
        Brushes.Red, Brushes.Blue, Brushes.Orange
    };

    private PieceType _currentPiece;
    private PieceType _nextPiece;
    private int _dropTimer = 0;
    private int _dropInterval = 30;
    private bool[,] _board = new bool[BoardHeight, BoardWidth];
    private int _score = 0;
    private int _lines = 0;
    private bool _gameOver;
    private DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public TetrisGame()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Focusable = true;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _currentPiece = (PieceType)new Random().Next(7);
        _nextPiece = (PieceType)new Random().Next(7);
        ResetButton.Click += OnStartClicked;
    }

    private void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        _board = new bool[BoardHeight, BoardWidth];
        _score = 0;
        _lines = 0;
        _gameOver = false;
        _currentPiece = _nextPiece;
        _nextPiece = (PieceType)new Random().Next(7);
        _timer.Start();
        RenderBoard();
        UpdateScore();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_gameOver) return;

        _dropTimer++;
        if (_dropTimer >= _dropInterval)
        {
            _dropTimer = 0;
            MovePiece(1, 0);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_gameOver) return;

        switch (e.Key)
        {
            case Key.Left: MovePiece(0, -1); break;
            case Key.Right: MovePiece(0, 1); break;
            case Key.Down: MovePiece(1, 0); break;
            case Key.Up: RotatePiece(); break;
            case Key.Space: HardDrop(); break;
        }
        RenderBoard();
    }

    private Position[] GetShape(PieceType type, int rotation)
    {
        return Shapes[(int)type][rotation];
    }

    private bool IsValid(PieceType type, int rotation, int row, int col)
    {
        var shape = GetShape(type, rotation);
        foreach (var pos in shape)
        {
            var r = row + pos.Row;
            var c = col + pos.Col;
            if (r < 0 || r >= BoardHeight || c < 0 || c >= BoardWidth) return false;
            if (_board[r, c]) return false;
        }
        return true;
    }

    private void MovePiece(int dRow, int dCol)
    {
        var shape = GetShape(_currentPiece, 0);
        var newRow = shape[0].Row + dRow;
        var newCol = shape[0].Col + dCol;

        if (IsValid(_currentPiece, 0, newRow, newCol))
        {
            foreach (var pos in shape)
            {
                _board[newRow + pos.Row - shape[0].Row, newCol + pos.Col - shape[0].Col] = true;
            }
        }
        else
        {
            LockPiece();
        }
    }

    private void RotatePiece()
    {
        var shape = GetShape(_currentPiece, 0);
        var newRotation = (0 + 1) % 4;
        if (IsValid(_currentPiece, newRotation, shape[0].Row, shape[0].Col))
        {
            foreach (var pos in shape)
            {
                _board[shape[0].Row + pos.Row, shape[0].Col + pos.Col] = false;
            }
        }
    }

    private void HardDrop()
    {
        var shape = GetShape(_currentPiece, 0);
        while (IsValid(_currentPiece, 0, shape[0].Row + 1, shape[0].Col))
        {
            foreach (var pos in shape)
            {
                _board[shape[0].Row + pos.Row, shape[0].Col + pos.Col] = false;
            }
            shape[0] = new Position(shape[0].Row + 1, shape[0].Col);
        }
        LockPiece();
    }

    private void LockPiece()
    {
        var shape = GetShape(_currentPiece, 0);
        foreach (var pos in shape)
        {
            _board[pos.Row, pos.Col] = true;
        }

        var linesCleared = 0;
        for (var r = BoardHeight - 1; r >= 0; r--)
        {
            if (GetRow(_board, r).All(x => x))
            {
                for (var rr = r; rr > 0; rr--)
                {
                    for (var c = 0; c < BoardWidth; c++)
                    {
                        _board[rr, c] = _board[rr - 1, c];
                    }
                }
                for (var c = 0; c < BoardWidth; c++)
                    _board[0, c] = false;
                linesCleared++;
                r++;
            }
        }

        if (linesCleared > 0)
        {
            _score += linesCleared * 100;
            _lines += linesCleared;
            _dropInterval = Math.Max(5, 30 - _lines / 10);
        }

        _currentPiece = _nextPiece;
        _nextPiece = (PieceType)new Random().Next(7);
        _dropTimer = 0;

        if (!IsValid(_currentPiece, 0, 0, 3))
        {
            _gameOver = true;
            StatusText.Text = "Game Over!";
            _timer.Stop();
        }
    }

    private void UpdateScore()
    {
        ScoreText.Text = $"Score: {_score}";
    }

    private void RenderBoard()
    {
        // Clear the GameBoard Border's content
        if (GameBoard != null)
        {
            GameBoard.Child = null;
        }

        var canvas = new Canvas { Width = BoardWidth * CellSize, Height = BoardHeight * CellSize };

        for (var r = 0; r < BoardHeight; r++)
        {
            for (var c = 0; c < BoardWidth; c++)
            {
                var rect = new Rectangle
                {
                    Width = CellSize - 1,
                    Height = CellSize - 1,
                    Fill = _board[r, c] ? Brushes.LightBlue : Brushes.DarkGray,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5,
                };
                Canvas.SetLeft(rect, c * CellSize);
                Canvas.SetTop(rect, r * CellSize);
                canvas.Children.Add(rect);
            }
        }

        if (GameBoard != null)
        {
            GameBoard.Child = canvas;
        }
        UpdateScore();
    }

    private static bool[] GetRow(bool[,] board, int row)
    {
        var result = new bool[board.GetLength(1)];
        for (var c = 0; c < board.GetLength(1); c++)
            result[c] = board[row, c];
        return result;
    }
}
