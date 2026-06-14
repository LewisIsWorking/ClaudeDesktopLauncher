using Avalonia.Controls;
using Avalonia.Input;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// One card representing a launcher-managed Claude slot in the grid.
/// Hosted by <see cref="SlotInstanceList"/>'s <c>WrapPanel</c>
/// <c>ItemsPanelTemplate</c>. DataContext is <c>ClaudeInstanceViewModel</c>.
///
/// <para>
/// Card contents (top-to-bottom): 240x150 thumbnail preview, slot pill
/// + login-status pill row, editable nickname, CPU/RAM/uptime stats
/// row + kill button. Width is fixed at 256px so the grid tiles
/// cleanly at 2 columns on default window widths and collapses to 1
/// column on narrow windows.
/// </para>
///
/// <para>
/// Click on the thumbnail area routes through
/// <see cref="OnThumbnailPressed"/> which invokes the VM's
/// <c>ShowPreviewCommand</c>. v1.9.2 switched from <c>Tapped</c> to
/// <c>PointerPressed</c> because <c>Tapped</c> on a <c>Border</c>
/// inside a <c>DataTemplate</c> wasn't firing reliably in Avalonia
/// 12 - <c>PointerPressed</c> is the lower-level event that fires
/// unconditionally on pointer-down. The <c>MouseButton.Left</c>
/// check filters out right-clicks so a future context menu can live
/// on the same control.
/// </para>
/// </summary>
public partial class SlotCard : UserControl
{
    public SlotCard()
    {
        InitializeComponent();
    }

    private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind
            != PointerUpdateKind.LeftButtonPressed) return;
        if (DataContext is ClaudeInstanceViewModel vm &&
            vm.ShowPreviewCommand.CanExecute(null))
        {
            vm.ShowPreviewCommand.Execute(null);
        }
    }
}
