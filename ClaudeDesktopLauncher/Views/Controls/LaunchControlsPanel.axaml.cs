using Avalonia.Controls;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// The top chrome of the launcher: the "Additional instances to open"
/// numeric control, the Launch Claude button, the Claude-not-installed
/// warning panel (shown when <c>IsClaudeInstalled</c> is false), the
/// "Launch on startup" + "Refresh interval" settings row, and the
/// update-available banner. DataContext is <c>MainWindowViewModel</c>.
/// Split out from MainWindow.axaml in v1.8.2 so the main window can
/// stay below the 200-line per-file limit.
/// </summary>
public partial class LaunchControlsPanel : UserControl
{
    public LaunchControlsPanel()
    {
        InitializeComponent();
    }
}
