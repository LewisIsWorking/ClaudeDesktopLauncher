using Avalonia.Controls;
using Avalonia.Input;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// One card representing an externally-launched Claude window in the
/// grid. Hosted by <see cref="ExternalInstanceList"/>'s <c>WrapPanel</c>
/// <c>ItemsPanelTemplate</c>. DataContext is <c>ExternalInstanceViewModel</c>.
///
/// <para>
/// Structurally mirrors <see cref="SlotCard"/> but is de-emphasised:
/// grayer border (#2A2A2A), darker background (#151515), thumbnail at
/// 0.85 opacity, muted foreground colors. Shows an "External" badge +
/// PID instead of a slot pill, and a trimmed command line with a
/// full-text tooltip. Close button uses the existing confirm-dialog
/// flow owned by the parent list VM.
/// </para>
///
/// <para>
/// Tap-to-enlarge uses the same <c>PointerPressed</c> pattern as
/// <see cref="SlotCard"/>. The VM type differs but the command
/// surface is identical.
/// </para>
/// </summary>
public partial class ExternalCard : UserControl
{
    public ExternalCard()
    {
        InitializeComponent();
    }

    private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind
            != PointerUpdateKind.LeftButtonPressed) return;
        if (DataContext is ExternalInstanceViewModel vm &&
            vm.ShowPreviewCommand.CanExecute(null))
        {
            vm.ShowPreviewCommand.Execute(null);
        }
    }
}
