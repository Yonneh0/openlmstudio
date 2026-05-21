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

public partial class SnakeGame : UserControl
{
    private const int GridSize = 20;
    private const int CellSize = 20;

    private record Position(int X, int Y);
    private Position _snakeHead = new(10, 10);
    private List<Position> _snake = new() { new(10, 10) };
    private Position _food = new(15, 15);
    private int _directionX = 1;
    private int _directionY = 0;
    private int _score = 0;
    private bool _gameOver;
    private DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private Canvas _canvas = new();

    public SnakeGame()
    {
        InitializeComponent();
        _gameTimer.Tick += OnTick;
        Focusable = true;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ResetButton.Click += OnStartClicked;
    }

    private void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        _snake = new List<Position> { new(10, 10) };
        _snakeHead = new(10, 10);
        _directionX = 1;
        _directionY = 0;
        _score = 0;
        _gameOver = false;
        PlaceFood();
        _gameTimer.Start();
        RenderGame();
        UpdateScore();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_gameOver) return;

        var newX = _snakeHead.X + _directionX;
        var newY = _snakeHead.Y + _directionY;

        if (newX < 0 || newX >= GridSize || newY < 0 || newY >= GridSize)
        {
            _gameOver = true;
            StatusText.Text = "Game Over!";
            _gameTimer.Stop();
            RenderGame();
            return;
        }

        var newHead = new Position(newX, newY);

        if (_snake.Any(s => s.X == newX && s.Y == newY))
        {
            _gameOver = true;
            StatusText.Text = "Game Over!";
            _gameTimer.Stop();
            RenderGame();
            return;
        }

        _snake.Insert(0, newHead);
        _snakeHead = newHead;

        if (newX == _food.X && newY == _food.Y)
        {
            _score++;
            PlaceFood();
            UpdateScore();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        RenderGame();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_gameOver) return;

        switch (e.Key)
        {
            case Key.Up when _directionY != 1: _directionX = 0; _directionY = -1; break;
            case Key.Down when _directionY != -1: _directionX = 0; _directionY = 1; break;
            case Key.Left when _directionX != 1: _directionX = -1; _directionY = 0; break;
            case Key.Right when _directionX != -1: _directionX = 1; _directionY = 0; break;
        }
    }

    private void PlaceFood()
    {
        var random = new Random();
        while (true)
        {
            var x = random.Next(GridSize);
            var y = random.Next(GridSize);
            if (!_snake.Any(s => s.X == x && s.Y == y))
            {
                _food = new Position(x, y);
                return;
            }
        }
    }

    private void UpdateScore()
    {
        ScoreText.Text = $"Score: {_score}";
    }

    private void RenderGame()
    {
        // Clear the GameBoard Border's content
        if (GameBoard != null)
        {
            GameBoard.Child = null;
        }

        _canvas = new Canvas { Width = GridSize * CellSize, Height = GridSize * CellSize };

        // Draw grid
        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                var rect = new Rectangle
                {
                    Width = CellSize - 1,
                    Height = CellSize - 1,
                    Fill = Brushes.DarkGreen,
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5,
                };
                Canvas.SetLeft(rect, x * CellSize);
                Canvas.SetTop(rect, y * CellSize);
                _canvas.Children.Add(rect);
            }
        }

        // Draw food
        var foodRect = new Rectangle
        {
            Width = CellSize - 2,
            Height = CellSize - 2,
            Fill = Brushes.Red,
            RadiusX = 5,
            RadiusY = 5,
        };
        Canvas.SetLeft(foodRect, _food.X * CellSize + 1);
        Canvas.SetTop(foodRect, _food.Y * CellSize + 1);
        _canvas.Children.Add(foodRect);

        // Draw snake
        for (var i = 0; i < _snake.Count; i++)
        {
            var pos = _snake[i];
            var rect = new Rectangle
            {
                Width = CellSize - 2,
                Height = CellSize - 2,
                Fill = i == 0 ? Brushes.Green : Brushes.LightGreen,
                RadiusX = 3,
                RadiusY = 3,
            };
            Canvas.SetLeft(rect, pos.X * CellSize + 1);
            Canvas.SetTop(rect, pos.Y * CellSize + 1);
            _canvas.Children.Add(rect);
        }

        if (GameBoard != null)
        {
            GameBoard.Child = _canvas;
        }
    }
}