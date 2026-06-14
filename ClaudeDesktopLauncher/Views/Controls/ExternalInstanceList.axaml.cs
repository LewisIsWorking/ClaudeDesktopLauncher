using Avalonia.Controls;

namespace ClaudeDesktopLauncher.Views.Controls;

/// <summary>
/// The "External Claude instances" section at the bottom of the main
/// window: the header row with totals, followed by an ItemsControl that
/// renders one row per external (non-launcher-managed) Claude process.
/// Visible only when <c>ExternalInstances.HasExternalInstances</c> is
/// true. DataContext is <c>MainWindowViewModel</c>; the per-row
/// template binds to <c>ExternalInstanceViewModel</c>. Split out from
/// MainWindow.axaml in v1.8.2.
/// </summary>
public partial class ExternalInstanceList : UserControl
{
    public ExternalInstanceList()
    {
        InitializeComponent();
    }
}
