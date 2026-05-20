using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models.Pingu;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Interactive Pingu avatar that reacts to Pingu state changes.
/// </summary>
public partial class PinguAvatar : UserControl
{
    private readonly IPinguStore _pingu;
    private readonly Timer? _mouthTimer;
    private int _mouthFrame;

    public PinguAvatar(IPinguStore pingu)
    {
        _pingu = pingu;
        InitializeComponent();
        _pingu.OnStateChanged += OnPinguStateChanged;

        _mouthTimer = new Timer(OnMouthTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void OnPinguStateChanged(object? sender, PinguStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateMoodDisplay(e.State);
            UpdateAnimation(e.State);
        });
    }

    private void UpdateMoodDisplay(PinguState state)
    {
        // Update mouth based on mood
        Mouth.Fill = state.Mood == PinguMood.Speaking
            ? new SolidColorBrush(Color.FromRgb(255, 80, 80))
            : new SolidColorBrush(Colors.Black);

        // Update body color based on mood
        var bodyColor = state.Mood switch
        {
            PinguMood.Thinking => Color.FromRgb(100, 149, 237),
            PinguMood.Happy => Color.FromRgb(255, 215, 0),
            PinguMood.Error => Color.FromRgb(255, 80, 80),
            PinguMood.Working => Color.FromRgb(144, 238, 144),
            _ => Colors.White
        };
        Body.Fill = new SolidColorBrush(bodyColor);
    }

    private void UpdateAnimation(PinguState state)
    {
        // Blink
        if (state.IsBlinking)
            BlinkOverlay.Opacity = 0.8;
        else
            BlinkOverlay.Opacity = 0;

        // Mouth animation for speaking
        if (state.Mood == PinguMood.Speaking)
            _mouthTimer?.Change(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1));
        else
            _mouthTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        // Bob animation
        var bobSpeed = state.BobSpeed;
        if (bobSpeed != 1.0)
        {
            var scaleX = 1.0 + 0.02 * Math.Sin(DateTime.UtcNow.Millisecond * bobSpeed / 100.0);
            var scaleY = 1.0 + 0.02 * Math.Cos(DateTime.UtcNow.Millisecond * bobSpeed / 100.0);
            var scale = new ScaleTransform(scaleX, scaleY);
            Body.RenderTransform = new TransformGroup { Children = new Transforms { scale } };
        }
        else
        {
            Body.RenderTransform = null;
        }
    }

    private void OnMouthTick(object? state)
    {
        _mouthFrame = (_mouthFrame + 1) % 4;
        Mouth.Height = 4 + _mouthFrame * 2;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = _pingu.ToggleMenuAsync();
    }
}
