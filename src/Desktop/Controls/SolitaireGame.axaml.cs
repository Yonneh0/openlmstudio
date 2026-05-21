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

public partial class SolitaireGame : UserControl
{
    private const int CardWidth = 70;
    private const int CardHeight = 98;
    private const int CardOverlap = 25;

    private enum Suit { Hearts, Diamonds, Clubs, Spades }
    private enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    private class Card { public Suit Suit; public Rank Rank; public bool FaceUp; }
    private record Position(double X, double Y);

    private readonly Random _random = new();
    private readonly List<Card> _stock = new();
    private readonly List<Card> _waste = new();
    private readonly List<List<Card>> _foundations = new(4) { new(), new(), new(), new() };
    private readonly List<List<Card>> _tableau = new(7) { new(), new(), new(), new(), new(), new(), new() };
    private Card? _dragging = null;
    private Position _dragOffset = new(0, 0);
    private Point _mousePos = new(0, 0);

    private static readonly string[] SuitSymbols = { "\u2665", "\u2666", "\u2663", "\u2660" };
    private static readonly IBrush[] SuitColors = { Brushes.Red, Brushes.Red, Brushes.Black, Brushes.Black };

    public SolitaireGame()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        NewGameButton.Click += OnNewGameClicked;
        GameBoard.AddHandler(PointerPressedEvent, OnBoardMouseDown, handledEventsToo: true);
        GameBoard.AddHandler(PointerMovedEvent, OnBoardMouseMove, handledEventsToo: true);
        GameBoard.AddHandler(PointerReleasedEvent, OnBoardMouseUp, handledEventsToo: true);
        NewGame();
    }

    private void OnNewGameClicked(object? sender, RoutedEventArgs e)
    {
        NewGame();
    }

    private void NewGame()
    {
        _stock.Clear();
        _waste.Clear();
        _foundations.Clear();
        _foundations.Add(new List<Card>());
        _foundations.Add(new List<Card>());
        _foundations.Add(new List<Card>());
        _foundations.Add(new List<Card>());
        _tableau.Clear();
        for (var i = 0; i < 7; i++)
            _tableau.Add(new List<Card>());
        _dragging = null;

        var deck = new List<Card>();
        foreach (var suit in Enum.GetValues<Suit>())
            foreach (var rank in Enum.GetValues<Rank>())
                deck.Add(new Card { Suit = suit, Rank = rank, FaceUp = false });

        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        for (var col = 0; col < 7; col++)
        {
            var pile = new List<Card>();
            for (var row = 0; row <= col; row++)
            {
                var card = deck[0];
                deck.RemoveAt(0);
                if (row == col) card.FaceUp = true;
                pile.Add(card);
            }
            _tableau.Add(pile);
        }

        _stock.AddRange(deck);
    }

    private void OnBoardMouseDown(object? sender, PointerEventArgs e)
    {
        _mousePos = e.GetPosition(GameBoard);

        var wasteX = 50;
        var wasteY = 20;
        if (Math.Abs(_mousePos.X - (wasteX + CardWidth / 2)) < CardWidth / 2 &&
            Math.Abs(_mousePos.Y - (wasteY + CardHeight / 2)) < CardHeight / 2 && _waste.Count > 0)
        {
            _dragging = _waste.Last();
            _dragOffset = new Position(_mousePos.X - wasteX, _mousePos.Y - wasteY);
            _waste.RemoveAt(_waste.Count - 1);
            return;
        }

        var tableauStartX = 50;
        var tableauStartY = 140;
        for (var col = 0; col < 7; col++)
        {
            var pile = _tableau[col];
            if (pile.Count == 0) continue;

            var card = pile.Last();
            if (!card.FaceUp) continue;

            var x = tableauStartX + col * (CardWidth - CardOverlap);
            var y = tableauStartY + (pile.Count - 1) * CardOverlap;

            if (Math.Abs(_mousePos.X - (x + CardWidth / 2)) < CardWidth / 2 &&
                Math.Abs(_mousePos.Y - (y + CardHeight / 2)) < CardHeight / 2)
            {
                _dragging = card;
                _dragOffset = new Position(_mousePos.X - x, _mousePos.Y - y);
                _tableau[col].RemoveAt(pile.Count - 1);
                return;
            }
        }

        var stockX = 50;
        var stockY = 20;
        if (Math.Abs(_mousePos.X - (stockX + CardWidth / 2)) < CardWidth / 2 &&
            Math.Abs(_mousePos.Y - (stockY + CardHeight / 2)) < CardHeight / 2)
        {
            DrawCards();
            return;
        }
    }

    private void OnBoardMouseMove(object? sender, PointerEventArgs e)
    {
        _mousePos = e.GetPosition(GameBoard);
        Render();
    }

    private void OnBoardMouseUp(object? sender, PointerEventArgs e)
    {
        if (_dragging == null) return;

        var x = _mousePos.X - _dragOffset.X;
        var y = _mousePos.Y - _dragOffset.Y;

        var foundationX = 350;
        var foundationY = 20;
        for (var i = 0; i < 4; i++)
        {
            var fx = foundationX + i * (CardWidth + 5);
            if (Math.Abs(x - fx) < CardWidth / 2 && Math.Abs(y - foundationY) < CardHeight / 2)
            {
                if (TryPlaceFoundation(_dragging, i))
                {
                    _foundations[i].Add(_dragging);
                    _dragging = null;
                    Render();
                    return;
                }
            }
        }

        var tableauStartX = 50;
        for (var col = 0; col < 7; col++)
        {
            var pile = _tableau[col];
            var tx = tableauStartX + col * (CardWidth - CardOverlap);

            if (Math.Abs(x - tx) < CardWidth / 2)
            {
                if (TryPlaceTableau(_dragging, pile))
                {
                    pile.Add(_dragging);
                    _dragging = null;
                    Render();
                    return;
                }
            }
        }

        _waste.Add(_dragging);
        _dragging = null;
        Render();
    }

    private bool TryPlaceFoundation(Card card, int pileIndex)
    {
        var pile = _foundations[pileIndex];
        if (pile.Count == 0)
            return card.Rank == Rank.Ace;
        var top = pile.Last();
        return top.Suit == card.Suit && top.Rank == (Rank)((int)card.Rank - 1);
    }

    private bool TryPlaceTableau(Card card, List<Card> pile)
    {
        if (pile.Count == 0)
            return card.Rank == Rank.King;
        var top = pile.Last();
        return top.FaceUp && top.Suit != card.Suit && top.Rank == (Rank)((int)card.Rank + 1);
    }

    private void DrawCards()
    {
        if (_stock.Count == 0)
        {
            _stock.AddRange(_waste.AsEnumerable().Reverse());
            _waste.Clear();
        }
        else
        {
            var count = Math.Min(3, _stock.Count);
            for (var i = 0; i < count; i++)
            {
                var card = _stock[_stock.Count - 1];
                _stock.RemoveAt(_stock.Count - 1);
                card.FaceUp = true;
                _waste.Add(card);
            }
        }
        Render();
    }

    private void Render()
    {
        GameBoard.Children.Clear();

        DrawCard(50, 20, null, false);

        for (var i = Math.Max(0, _waste.Count - 3); i < _waste.Count; i++)
        {
            var card = _waste[i];
            DrawCard(50 + (i - Math.Max(0, _waste.Count - 3)) * 15, 20, card, true);
        }

        for (var i = 0; i < 4; i++)
        {
            var fx = 350 + i * (CardWidth + 5);
            var fy = 20;
            if (_foundations[i].Count > 0)
                DrawCard(fx, fy, _foundations[i].Last(), true);
            else
                DrawCard(fx, fy, null, false);
        }

        var startX = 50;
        var startY = 140;
        for (var col = 0; col < 7; col++)
        {
            var pile = _tableau[col];
            for (var row = 0; row < pile.Count; row++)
            {
                var card = pile[row];
                var x = startX + col * (CardWidth - CardOverlap);
                var y = startY + row * CardOverlap;
                if (_dragging != null && _dragging == card) continue;
                DrawCard(x, y, card, card.FaceUp);
            }
        }

        if (_dragging != null)
        {
            var x = _mousePos.X - _dragOffset.X;
            var y = _mousePos.Y - _dragOffset.Y;
            DrawCard(x, y, _dragging, true);
        }
    }

    private void DrawCard(double x, double y, Card? card, bool faceUp)
    {
        var rect = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = CardWidth - 2,
            Height = CardHeight - 2,
            Fill = faceUp ? Brushes.White : Brushes.Blue,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            RadiusX = 5,
            RadiusY = 5,
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        GameBoard.Children.Add(rect);

        if (faceUp && card != null)
        {
            var c = card;
            var color = SuitColors[(int)c.Suit];
            var symbol = SuitSymbols[(int)c.Suit];
            var rankStr = c.Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)c.Rank).ToString()
            };

            var rankText = new TextBlock
            {
                Text = rankStr,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = color,
            };
            Canvas.SetLeft(rankText, x + 4);
            Canvas.SetTop(rankText, y + 4);
            GameBoard.Children.Add(rankText);

            var suitText = new TextBlock
            {
                Text = symbol,
                FontSize = 14,
                Foreground = color,
            };
            Canvas.SetLeft(suitText, x + 4);
            Canvas.SetTop(suitText, y + 20);
            GameBoard.Children.Add(suitText);

            var centerText = new TextBlock
            {
                Text = symbol,
                FontSize = 36,
                Foreground = color,
            };
            Canvas.SetLeft(centerText, x + CardWidth / 2 - 15);
            Canvas.SetTop(centerText, y + CardHeight / 2 - 18);
            GameBoard.Children.Add(centerText);
        }
    }
}