namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Captures a thumbnail-sized PNG snapshot of a target process's main
/// window. Returns PNG bytes rather than an Avalonia <c>Bitmap</c> so
/// the Core layer stays free of Avalonia dependencies - the UI layer
/// is responsible for materialising an <c>IImage</c> from the bytes
/// when it's time to render.
///
/// <para>
/// Implementations must return <c>null</c> (and not throw) for any of
/// these recoverable cases:
/// </para>
/// <list type="bullet">
/// <item>The process is no longer running (raced with exit).</item>
/// <item>The process has no visible main window (close-to-tray state).</item>
/// <item>Capture failed for a transient reason (GDI resource pressure,
/// etc.). The caller will retry on the next poll tick.</item>
/// </list>
///
/// <para>
/// A return value of <c>null</c> is a signal to the caller to
/// <b>preserve</b> whatever thumbnail the UI currently shows, not to
/// blank it out - so a slot that close-to-tray's retains the last
/// captured frame with a "Hidden" overlay applied by the view.
/// </para>
/// </summary>
public interface IWindowThumbnailService
{
    /// <summary>
    /// Capture the target process's main window as a PNG at roughly
    /// <paramref name="width"/> x <paramref name="height"/>. The
    /// aspect ratio is preserved from the source window and the result
    /// is scaled to fit within the requested box, so the actual image
    /// dimensions may be smaller on one axis.
    /// </summary>
    /// <returns>PNG bytes, or <c>null</c> if no window was available
    /// or capture failed.</returns>
    byte[]? CapturePngBytes(int processId, int width, int height);
}
