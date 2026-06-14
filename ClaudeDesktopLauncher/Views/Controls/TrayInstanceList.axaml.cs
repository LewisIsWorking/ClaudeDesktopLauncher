using Avalonia.Controls;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// The "Hidden / tray" section for launcher-managed Claude slots that
/// have been close-to-tray'd. Shows only when
/// <c>SlotInstances.HasTrayItems</c> is true. Each tray row shows the
/// Slot N pill, the nickname (read-only - the user can't see the
/// underlying window to verify the edit), live CPU/RAM/uptime, and a
/// "Quit" button that force-kills the process tree (no confirm dialog -
/// the slot has no visible window whose state could be lost). Split out
/// from MainWindow.axaml in v1.8.2.
/// </summary>
public partial class TrayInstanceList : UserControl
{
    public TrayInstanceList()
    {
        InitializeComponent();
    }
}
