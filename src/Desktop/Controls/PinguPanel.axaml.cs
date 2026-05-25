using Avalonia.Controls;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;
using OpenLMStudio.Infrastructure;

namespace OpenLMStudio.Desktop.Controls;

/// <summary>
/// Pingu overlay panel with tabs for Skills, Settings, Models, Compile, Logs, About.
/// </summary>
public partial class PinguPanel : UserControl
{
    #pragma warning disable CS0169
    private IPinguStore? _pingu;
    #pragma warning restore CS0169

    public PinguPanel()
    {
        InitializeComponent();
    }

    private void OnPinguStateChanged(object? sender, PinguStateChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var tabIndex = e.State.ActivePanel switch
            {
                PinguPanelType.Skills => 0,
                PinguPanelType.Settings => 1,
                PinguPanelType.Models => 2,
                PinguPanelType.Compile => 3,
                PinguPanelType.Logs => 4,
                PinguPanelType.About => 5,
                _ => 0
            };
            if (PanelTabs != null && PanelTabs.SelectedIndex != tabIndex)
                PanelTabs.SelectedIndex = tabIndex;
        });
    }
}
