using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// The "Running / Total RAM / Total CPU" summary row with trailing
/// Refresh (?), Copy screenshot, and Logs folder buttons. DataContext
/// is <c>MainWindowViewModel</c>. Split out from MainWindow.axaml in
/// v1.8.2.
///
/// <para>
/// The Copy button is non-trivially wired: clipboard bitmap capture
/// needs access to the <c>TopLevel</c> / <c>Window</c> that owns this
/// control, which a UserControl doesn't have directly. The control
/// therefore exposes a <see cref="CopyClicked"/> event; the hosting
/// <c>MainWindow</c> subscribes and performs the capture there, where
/// it has the window reference.
/// </para>
/// </summary>
public partial class ResourceTotalsRow : UserControl
{
    /// <summary>Raised when the user clicks the Copy button. The host
    /// window handles the actual screenshot + clipboard operation.</summary>
    public event EventHandler<RoutedEventArgs>? CopyClicked;

    public ResourceTotalsRow()
    {
        InitializeComponent();
        var copyButton = this.FindControl<Button>("CopyButton");
        if (copyButton is not null)
            copyButton.Click += (s, e) => CopyClicked?.Invoke(this, e);
    }
}
