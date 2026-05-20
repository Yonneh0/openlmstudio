using Avalonia.Controls;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// VM console output display.
/// </summary>
public partial class VMConsole : UserControl
{
    public VMConsole()
    {
        InitializeComponent();
    }

    public void AppendText(string text)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            ConsoleOutput.Text += text;
            ConsoleOutput.CaretIndex = ConsoleOutput.Text.Length;
        });
    }

    public void ClearText()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            ConsoleOutput.Clear();
        });
    }
}