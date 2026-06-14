using Avalonia.Controls;
using Avalonia.Input;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// One card representing a tray-resident (close-to-tray'd) Claude slot.
/// Hosted by <see cref="TrayInstanceList"/>'s <c>WrapPanel</c>
/// <c>ItemsPanelTemplate</c>. DataContext is <c>ClaudeInstanceViewModel</c>.
///
/// <para>
/// Structurally mirrors <see cref="SlotCard"/> but with three key
/// differences: the thumbnail carries a "Hidden" badge overlay and
/// shows at reduced opacity (to communicate stale / frozen state);
/// the nickname is read-only italic (the user cannot usefully edit a
/// name they cannot see in the slot window); the bottom button reads
/// "Quit" instead of "X" because there is no longer a visible window
/// whose state needs protecting behind a confirm dialog.
/// </para>
///
/// <para>
/// Tap-to-enlarge works on tray cards too - the preview service
/// falls back to the last captured frame when fresh capture fails
/// (which it will for tray-resident windows since their main window
/// handle is gone). Uses <c>PointerPressed</c> for the same reason
/// as <see cref="SlotCard"/>.
/// </para>
/// </summary>
public partial class TrayCard : UserControl
{
    public TrayCard()
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
