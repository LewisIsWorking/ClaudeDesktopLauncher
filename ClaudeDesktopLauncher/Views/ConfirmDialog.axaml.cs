using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Views;

/// <summary>
/// Modal confirmation dialog. Construct via <c>new ConfirmDialog()</c>,
/// call <see cref="Apply"/> with the desired options, then
/// <c>await ShowDialog(owner)</c>. When the dialog closes, inspect
/// <see cref="Result"/> to see the user's choice. Orchestration lives in
/// <see cref="Services.ConfirmDialogService"/>; this code-behind only
/// owns the visual and keyboard wiring.
/// </summary>
public partial class ConfirmDialog : Window
{
    /// <summary>
    /// True when the user confirmed, false otherwise. Starts false so any
    /// non-Confirm dismissal path (Esc, window close, Alt+F4) is treated
    /// as a cancel without additional wiring.
    /// </summary>
    public bool Result { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Applies <paramref name="options"/> to the dialog's visual tree.
    /// Kept separate from the constructor so the XAML-loader can still
    /// instantiate the window without options (needed for Avalonia's
    /// design-time support and for the preview pane in Rider).
    /// </summary>
    public void Apply(ConfirmDialogOptions options)
    {
        Title = options.Title;
        TitleText.Text = options.Title;
        MessageText.Text = options.Message;
        ConfirmButton.Content = options.ConfirmText;
        CancelButton.Content = options.CancelText;

        var (accentBrush, confirmForeground) = ColoursFor(options.Severity);
        SeverityAccent.Background = accentBrush;
        if (options.Severity == DialogSeverity.Destructive)
            ConfirmButton.Foreground = confirmForeground;
    }

    /// <summary>
    /// Brushes for the severity accent bar and, for destructive severity,
    /// the confirm button text. Constants rather than theme-resource
    /// lookups so the dialog is not brittle to theme dictionary changes.
    /// </summary>
    private static (IBrush accent, IBrush confirmForeground) ColoursFor(DialogSeverity severity) =>
        severity switch
        {
            DialogSeverity.Destructive =>
                (new SolidColorBrush(Color.FromRgb(0xDC, 0x14, 0x3C)),
                 new SolidColorBrush(Color.FromRgb(0xEF, 0x9A, 0x9A))),
            DialogSeverity.Warning =>
                (new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                 Brushes.White),
            _ =>
                (new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)),
                 Brushes.White)
        };

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    /// <summary>
    /// Esc cancels. Enter is handled automatically by the confirm
    /// button's <c>IsDefault="True"</c> so no override needed here.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = false;
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}