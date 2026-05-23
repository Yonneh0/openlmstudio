using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace OpenLMStudio.Desktop.Controls;

public partial class GamesPanel : UserControl
{
    private MinesweeperGame? _minesweeper;
    private TetrisGame? _tetris;
    private SnakeGame? _snake;
    private JezzballGame? _jezzball;
    private SolitaireGame? _solitaire;

    public GamesPanel()
    {
        InitializeComponent();
        GameSelector.SelectionChanged += OnGameSelected;
        CloseGameButton.Click += OnCloseGameClicked;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Auto-load the first game when the panel is shown
        if (GameSelector?.SelectedIndex == 0 && ActiveGameContainer?.Child == null)
        {
            _minesweeper ??= new MinesweeperGame();
            ActiveGameContainer.Child = _minesweeper;
        }
    }

    private void OnGameSelected(object? sender, SelectionChangedEventArgs e)
    {
        var index = GameSelector.SelectedIndex;
        ActiveGameContainer.Child = index switch
        {
            0 => _minesweeper ??= new MinesweeperGame(),
            1 => _tetris ??= new TetrisGame(),
            2 => _snake ??= new SnakeGame(),
            3 => _jezzball ??= new JezzballGame(),
            4 => _solitaire ??= new SolitaireGame(),
            _ => null
        };
    }

    private void OnCloseGameClicked(object? sender, RoutedEventArgs e)
    {
        ActiveGameContainer.Child = null;
    }

    public void ActivateGame(string gameName)
    {
        var index = gameName switch
        {
            "minesweeper" => 0,
            "tetris" => 1,
            "snake" => 2,
            "jezzball" => 3,
            "solitaire" => 4,
            _ => -1
        };

        if (index >= 0 && GameSelector != null)
        {
            GameSelector.SelectedIndex = index;
        }
    }
}
