using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenLMStudio.Desktop.Controls;

public partial class VMConsole : UserControl
{
    private const int TerminalFontSize = 13;
    private const int FontWidth = 8;
    private const int FontHeight = 16;
    private const int Columns = 100;
    private const int Rows = 40;

    private readonly List<ConsoleLine> _lines = new();
    private readonly StringBuilder _inputBuffer = new();
    private int _inputColumn = 0;
    private string _prompt = "user@vm:~$ ";
    private readonly Canvas _canvas;

    public VMConsole()
    {
        InitializeComponent();
        ClearButton.Click += OnClearClicked;
        CopyButton.Click += OnCopyClicked;
        _canvas = TerminalCanvas;
        _canvas.Width = Columns * FontWidth;
        _canvas.Height = Rows * FontHeight;
        Focusable = true;
        KeyDown += OnKeyDown;
    }

    public void AppendOutput(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (string.IsNullOrEmpty(text))
                return;

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                _lines.Add(new ConsoleLine(ParseAnsi(line)));
            }
            if (_lines.Count > 500)
                _lines.RemoveRange(0, _lines.Count - 500);
            Render();
            ScrollToBottom();
        });
    }

    public void AppendPrompt(string prompt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (string.IsNullOrEmpty(prompt))
                return;

            _prompt = prompt;
            _lines.Add(new ConsoleLine(new List<AnsiChar> { new(' ', 7) }));
            Render();
            ScrollToBottom();
        });
    }

    public void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lines.Clear();
            Render();
        });
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        Clear();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        var text = string.Join("\n", _lines.Select(l => l.Text));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            var data = new Avalonia.Input.DataTransfer();
            data.Add(Avalonia.Input.DataTransferItem.CreateText(text));
            _ = clipboard.SetDataAsync(data);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var command = _inputBuffer.ToString();
            _lines.Add(new ConsoleLine(new List<AnsiChar> { new(' ', 7) }));
            _lines.Add(new ConsoleLine(ParseAnsi(command)));
            _lines.Add(new ConsoleLine(ParseAnsi($"$ {command}\n")));
            _inputBuffer.Clear();
            _inputColumn = 0;
            Render();
            ScrollToBottom();
        }
        else if (e.Key == Key.Back && _inputBuffer.Length > 0)
        {
            _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
            _inputColumn = Math.Max(0, _inputColumn - 1);
            Render();
        }
        else if (e.Key == Key.Escape)
        {
            _inputBuffer.Clear();
            _inputColumn = 0;
            Render();
        }
        else if (e.Key == Key.Space)
        {
            _inputBuffer.Append(' ');
            _inputColumn++;
            Render();
        }
        else if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            var digit = (char)((int)Key.D0 + (int)e.Key - (int)Key.D0);
            _inputBuffer.Append(digit);
            _inputColumn++;
            Render();
        }
        else if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            _inputBuffer.Append(char.ToLower((char)((int)Key.A + (int)e.Key - (int)Key.A)));
            _inputColumn++;
            Render();
        }
        else if (e.Key == Key.Tab)
        {
            var spaces = 4 - (_inputColumn % 4);
            for (var s = 0; s < spaces; s++)
            {
                _inputBuffer.Append(' ');
                _inputColumn++;
            }
            Render();
        }
        else if (e.Key == Key.Left)
        {
            _inputColumn = Math.Max(0, _inputColumn - 1);
            Render();
        }
        else if (e.Key == Key.Right)
        {
            _inputColumn = Math.Min(_inputBuffer.Length, _inputColumn + 1);
            Render();
        }
    }

    private List<AnsiChar> ParseAnsi(string text)
    {
        var chars = new List<AnsiChar>();
        var i = 0;
        var fgColor = 7;
        var bgColor = 0;

        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var j = i + 2;
                var seq = new StringBuilder();
                while (j < text.Length && char.IsAsciiLetter(text[j]))
                {
                    seq.Append(text[j]);
                    j++;
                }

                var paramsStr = text.Substring(i + 2, j - i - 2);
                if (paramsStr == "m")
                {
                    var codes = seq.Length > 0 ? seq.ToString() : paramsStr;
                    foreach (var code in codes.Split(';'))
                    {
                        if (int.TryParse(code, out var c))
                        {
                            if (c == 0) { fgColor = 7; bgColor = 0; }
                            else if (c <= 37) fgColor = c - 30;
                            else if (c <= 47) bgColor = c - 40;
                            else if (c == 1) fgColor = Math.Min(15, fgColor + 8);
                        }
                    }
                }
                i = j;
            }
            else if (text[i] == '\n')
            {
                chars.Add(new AnsiChar('\n', fgColor, bgColor));
                i++;
            }
            else if (text[i] == '\r')
            {
                i++;
            }
            else if (text[i] == '\t')
            {
                var spaces = 4 - (_inputColumn % 4);
                for (var s = 0; s < spaces; s++)
                    chars.Add(new AnsiChar(' ', fgColor, bgColor));
                i++;
            }
            else
            {
                chars.Add(new AnsiChar(text[i], fgColor, bgColor));
                i++;
            }
        }

        return chars;
    }

    private void Render()
    {
        _canvas.Children.Clear();

        var startY = Math.Max(0, _lines.Count - Rows);
        for (var row = 0; row < Rows && (startY + row) < _lines.Count; row++)
        {
            var line = _lines[startY + row];
            for (var col = 0; col < line.Chars.Count && col < Columns; col++)
            {
                var ch = line.Chars[col];
                if (ch.Char == '\0') continue;

                var text = new TextBlock
                {
                    Text = ch.Char == '\n' ? "" : ch.Char.ToString(),
                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                    FontSize = TerminalFontSize,
                    Foreground = GetBrush(ch.FgColor),
                };
                Canvas.SetLeft(text, col * FontWidth);
                Canvas.SetTop(text, row * FontHeight);

                _canvas.Children.Add(text);
            }
        }

        // Render input line
        var inputRow = Rows - 1;
        var inputCol = 0;

        // Prompt
        for (var i = 0; i < _prompt.Length && inputCol < Columns; i++)
        {
            var text = new TextBlock
            {
                Text = _prompt[i].ToString(),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = TerminalFontSize,
                Foreground = Brushes.Yellow,
            };
            Canvas.SetLeft(text, inputCol * FontWidth);
            Canvas.SetTop(text, inputRow * FontHeight);
            _canvas.Children.Add(text);
            inputCol++;
        }

        // Input buffer
        for (var i = 0; i < _inputBuffer.Length && inputCol < Columns; i++)
        {
            var text = new TextBlock
            {
                Text = _inputBuffer[i].ToString(),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = TerminalFontSize,
                Foreground = Brushes.White,
            };
            Canvas.SetLeft(text, inputCol * FontWidth);
            Canvas.SetTop(text, inputRow * FontHeight);
            _canvas.Children.Add(text);
            inputCol++;
        }

        // Cursor
        var cursorX = (inputCol) * FontWidth;
        var cursorY = inputRow * FontHeight;
        var cursorRect = new Rectangle
        {
            Width = FontWidth - 1,
            Height = FontHeight - 2,
            Fill = Brushes.White,
        };
        Canvas.SetLeft(cursorRect, cursorX);
        Canvas.SetTop(cursorRect, cursorY);
        _canvas.Children.Add(cursorRect);
    }

    private static Brush GetBrush(int color, bool isBackground = false)
    {
        var colors = new[]
        {
            Brushes.Black, Brushes.DarkRed, Brushes.DarkGreen, Brushes.Brown,
            Brushes.DarkBlue, Brushes.Purple, Brushes.Teal, Brushes.LightGray,
            Brushes.Gray, Brushes.Red, Brushes.Green, Brushes.Yellow,
            Brushes.Blue, Brushes.Magenta, Brushes.Cyan, Brushes.White,
        };

        return isBackground && color > 0 ? (Brush)(object)colors[color] : (Brush)Brushes.Transparent;
    }

    private void ScrollToBottom()
    {
        if (TerminalScrollViewer is ScrollViewer sv)
        {
            var content = sv.Content;
            if (content is Panel p)
            {
                var height = p.Bounds.Height;
                sv.Offset = new Vector(sv.Offset.X, height);
            }
        }
    }

    private record ConsoleLine(List<AnsiChar> Chars)
    {
        public string Text => new(Chars.Where(c => c.Char != '\0' && c.Char != '\n').Select(c => c.Char).ToArray());
    }

    private record AnsiChar(char Char, int FgColor, int BgColor)
    {
        public AnsiChar(char c, int fg) : this(c, fg, 0) { }
    }
}