using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.Views;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Avalonia-backed <see cref="IThumbnailPreviewService"/>. Shows a
/// <see cref="ThumbnailPreviewWindow"/> owned by the main window so
/// it centres on the launcher and disappears if the launcher closes.
/// Dispatched on the UI thread so callers can trigger it from any
/// context without worrying about thread affinity.
///
/// <para>
/// v1.9.2: captures fresh at 1920x1200 via
/// <see cref="IWindowThumbnailService"/> so the preview is crisp
/// rather than an upscaled 240x150 thumbnail. Falls back to the
/// caller-supplied cached bitmap if fresh capture fails (tray-
/// resident window, PID exited, GDI pressure).
/// </para>
/// </summary>
public class AvaloniaThumbnailPreviewService : IThumbnailPreviewService
{
    /// <summary>
    /// Target dimensions for the fresh high-res capture. The
    /// <see cref="PrintWindowThumbnailService"/> scales letterboxed
    /// to fit so the actual delivered image may be smaller on one
    /// axis, but 1920x1200 gives plenty of headroom for the
    /// preview window's default 900x600 at any DPI scaling factor
    /// the user is likely to have.
    /// </summary>
    private const int PreviewCaptureWidth = 1920;
    private const int PreviewCaptureHeight = 1200;

    private readonly IWindowThumbnailService _captureService;
    private readonly ILoggingService _logger;

    public AvaloniaThumbnailPreviewService(
        IWindowThumbnailService captureService,
        ILoggingService logger)
    {
        _captureService = captureService;
        _logger = logger;
    }

    public void Show(int processId, Bitmap? fallback, string title)
    {
        var fresh = TryFreshCapture(processId);
        var bitmap = fresh ?? fallback;
        _logger.LogInfo(
            $"Preview requested: title='{title}' pid={processId} " +
            $"fresh={(fresh is null ? "null" : "ok")} " +
            $"fallback={(fallback is null ? "null" : "ok")}");
        if (bitmap is null) return;
        Dispatcher.UIThread.Post(() => OpenWindow(bitmap, title));
    }

    /// <summary>
    /// Attempts a fresh high-res capture via
    /// <see cref="IWindowThumbnailService"/>. Decodes the PNG bytes
    /// into an Avalonia <c>Bitmap</c> on the calling thread (safe -
    /// <c>Bitmap</c> construction doesn't require UI thread affinity).
    /// Returns null for any failure; the caller falls back to the
    /// cached thumbnail in that case.
    /// </summary>
    private Bitmap? TryFreshCapture(int processId)
    {
        var bytes = _captureService.CapturePngBytes(
            processId, PreviewCaptureWidth, PreviewCaptureHeight);
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                $"Fresh preview decode failed for PID {processId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Owner resolution: Avalonia's desktop-style app lifetime exposes
    /// the main window via <see cref="IClassicDesktopStyleApplicationLifetime.MainWindow"/>.
    /// We bind the preview to that as its owner so it centres on the
    /// launcher and participates in its z-order (closes when the
    /// launcher closes, doesn't linger as an orphan window).
    /// </summary>
    private void OpenWindow(Bitmap thumbnail, string title)
    {
        var owner = ResolveOwner();
        var win = new ThumbnailPreviewWindow { Title = title };
        win.SetThumbnail(thumbnail);
        if (owner is not null) win.Show(owner);
        else win.Show();
        _logger.LogInfo(
            $"Preview window shown (owner={(owner is null ? "none" : "main")}) for '{title}'");
    }

    private static Window? ResolveOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
