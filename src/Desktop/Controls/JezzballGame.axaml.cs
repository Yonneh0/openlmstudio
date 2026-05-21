using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenLMStudio.Desktop.Controls;

public partial class JezzballGame : UserControl
{
    private const int BoardWidth = 600;
    private const int BoardHeight = 400;
    private const int CellSize = 10;
    private const int GridWidth = BoardWidth / CellSize;
    private const int GridHeight = BoardHeight / CellSize;

    private record Rect(int X, int Y, int Width, int Height);
    private class Ball { public double X, Y, Vx, Vy; }

    private readonly Random _random = new();
    private readonly List<Rect> _rectangles = new();
    private readonly List<Ball> _balls = new();
    private bool[,] _grid = new bool[GridWidth, GridHeight];
    private int _score = 0;
    private int _totalCells = 0;
    private bool _drawing = false;
    private Point _startPoint;
    private DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    public JezzballGame()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        NewGameButton.Click += OnNewGameClicked;
        GameBoard.AddHandler(PointerPressedEvent, OnBoardClick, handledEventsToo: true);
        _timer.Tick += OnTick;
        _timer.Start();
        NewGame();
    }

    private void OnNewGameClicked(object? sender, RoutedEventArgs e)
    {
        NewGame();
    }

    private void NewGame()
    {
        _rectangles.Clear();
        _balls.Clear();
        _grid = new bool[GridWidth, GridHeight];
        _score = 0;
        _totalCells = 0;
        _drawing = false;

        AddRect(10, 10, 20, 15);
        AddRect(40, 20, 15, 20);

        AddBall();
        AddBall();

        UpdateScore();
    }

    private void AddRect(int x, int y, int w, int h)
    {
        _rectangles.Add(new Rect(x, y, w, h));
        for (var gy = y; gy < y + w; gy++)
            for (var gx = y; gx < y + h; gx++)
                if (gx < GridWidth && gy < GridHeight)
                    _grid[gy, gx] = true;
        _totalCells += w * h;
    }

    private void AddBall()
    {
        _balls.Add(new Ball
        {
            X = _random.Next(50, BoardWidth - 50),
            Y = _random.Next(50, BoardHeight - 50),
            Vx = (_random.NextDouble() > 0.5 ? 1 : -1) * (2 + _random.NextDouble() * 2),
            Vy = (_random.NextDouble() > 0.5 ? 1 : -1) * (2 + _random.NextDouble() * 2),
        });
    }

    private void OnTick(object? sender, EventArgs e)
    {
        for (var i = _balls.Count - 1; i >= 0; i--)
        {
            var ball = _balls[i];
            var newX = ball.X + ball.Vx;
            var newY = ball.Y + ball.Vy;

            if (newX <= 0 || newX >= BoardWidth) ball.Vx *= -1;
            if (newY <= 0 || newY >= BoardHeight) ball.Vy *= -1;

            newX = Math.Max(0, Math.Min(BoardWidth, newX));
            newY = Math.Max(0, Math.Min(BoardHeight, newY));

            var hit = false;
            foreach (var rect in _rectangles)
            {
                if (newX > rect.X * CellSize && newX < (rect.X + rect.Width) * CellSize &&
                    newY > rect.Y * CellSize && newY < (rect.Y + rect.Height) * CellSize)
                {
                    hit = true;
                    break;
                }
            }

            if (hit)
            {
                _balls.RemoveAt(i);
                AddBall();
                continue;
            }

            ball.X = newX;
            ball.Y = newY;
        }

        Render();
    }

    private void OnBoardClick(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(GameBoard);
        var gx = (int)(pos.X / CellSize);
        var gy = (int)(pos.Y / CellSize);

        if (!_drawing)
        {
            _drawing = true;
            _startPoint = pos;
        }
        else
        {
            var dx = Math.Max(0, gx - (int)_startPoint.X);
            var dy = Math.Max(0, gy - (int)_startPoint.Y);

            if (dx > 2 && dy > 2)
            {
                AddRect((int)_startPoint.X, (int)_startPoint.Y, dx, dy);
                _score += dx * dy;
                _totalCells += dx * dy;
                UpdateScore();

                if (_totalCells >= GridWidth * GridHeight * 0.95)
                {
                    Console.WriteLine($"You win! Score: {_score}");
                    NewGame();
                }
            }

            _drawing = false;
        }

        Render();
    }

    private void UpdateScore()
    {
        ScoreText.Text = $"Score: {_score}";
    }

    private void Render()
    {
        GameBoard.Children.Clear();

        foreach (var rect in _rectangles)
        {
            var r = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = rect.Width * CellSize - 1,
                Height = rect.Height * CellSize - 1,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.White,
                StrokeThickness = 1,
            };
            Canvas.SetLeft(r, rect.X * CellSize);
            Canvas.SetTop(r, rect.Y * CellSize);
            GameBoard.Children.Add(r);
        }

        foreach (var ball in _balls)
        {
            var c = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Red,
            };
            Canvas.SetLeft(c, ball.X - 5);
            Canvas.SetTop(c, ball.Y - 5);
            GameBoard.Children.Add(c);
        }
    }
}