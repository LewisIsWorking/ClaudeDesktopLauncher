using Avalonia.Controls;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// Scrolling list of launcher-managed Claude slot rows. Each row shows
/// the "Slot N" identity pill, the login-status pill, the editable
/// nickname, live CPU/RAM/uptime, and a kill button. DataContext is
/// <c>MainWindowViewModel</c>; the per-row template binds to
/// <c>ClaudeInstanceViewModel</c> via <c>SlotInstances.Items</c>.
/// Split out from MainWindow.axaml in v1.8.2.
/// </summary>
public partial class SlotInstanceList : UserControl
{
    public SlotInstanceList()
    {
        InitializeComponent();
    }
}
