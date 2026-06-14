using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace ClaudeDesktopLauncher.Views;

/// <summary>
/// Lightbox-style preview window for a slot or external thumbnail.
/// Opens from <c>AvaloniaThumbnailPreviewService.Show</c> with a
/// <see cref="Bitmap"/> and a title. Closes on any click or Esc, so
/// it feels lightweight rather than persistent.
///
/// <para>
/// The underlying bitmap is owned by the <see cref="ViewModels.IThumbnailableViewModel"/>
/// that spawned the preview; we deliberately do not Dispose it here
/// when the window closes because the source VM may still be using
/// it for its in-card thumbnail. The preview just holds a reference
/// for the duration of the window's lifetime.
/// </para>
/// </summary>
public partial class ThumbnailPreviewWindow : Window
{
    public ThumbnailPreviewWindow()
    {
        InitializeComponent();
        Root.PointerPressed += (_, _) => Close();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    /// <summary>
    /// Sets the thumbnail to display. Safe to call with null; the
    /// window shows an empty frame in that case (shouldn't happen in
    /// practice - the service filters nulls before opening).
    /// </summary>
    public void SetThumbnail(Bitmap? thumbnail)
    {
        PreviewImage.Source = thumbnail;
    }
}
