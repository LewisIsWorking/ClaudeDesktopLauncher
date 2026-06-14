using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Renders an Avalonia <see cref="Visual"/> to a bitmap via
/// <see cref="RenderTargetBitmap"/> and places it on the system clipboard
/// using the Avalonia 12 <c>ClipboardExtensions.SetBitmapAsync</c> helper.
/// SetBitmapAsync handles cross-platform clipboard-format registration
/// (CF_DIB / CF_BITMAP on Windows, equivalent on other platforms) so
/// paste compatibility is as good as any other Avalonia app.
///
/// Captures the visual tree only - native Windows chrome like the yellow
/// title bar is not included. For our purposes (release screenshots,
/// quick sharing of what the user sees) that is fine: the content is
/// what matters and this avoids the GDI/BitBlt approach that fails for
/// partially-obscured or off-screen windows.
///
/// Always fails gracefully (never throws): a denied clipboard, a closed
/// window, or a visual that rendered to zero pixels simply returns
/// <c>false</c> and logs the cause.
/// </summary>
public class WindowSnapshotService : IWindowSnapshotService
{
    private const int MinCaptureDimension = 2;

    private readonly ILoggingService _logger;

    public WindowSnapshotService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<bool> CaptureAndCopyAsync(Visual visual)
    {
        var size = GetPixelSize(visual);
        if (size.Width < MinCaptureDimension || size.Height < MinCaptureDimension)
        {
            _logger.LogWarning($"Visual too small to snapshot: {size}");
            return false;
        }

        var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
        if (clipboard is null)
        {
            _logger.LogWarning("Clipboard unavailable for this visual");
            return false;
        }

        try
        {
            using var rtb = new RenderTargetBitmap(size);
            rtb.Render(visual);
            await clipboard.SetBitmapAsync(rtb);
            _logger.LogInfo($"Window snapshot ({size}) copied to clipboard");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to copy window snapshot to clipboard", ex);
            return false;
        }
    }

    private static PixelSize GetPixelSize(Visual visual)
    {
        var width = (int)Math.Ceiling(visual.Bounds.Width);
        var height = (int)Math.Ceiling(visual.Bounds.Height);
        return new PixelSize(Math.Max(1, width), Math.Max(1, height));
    }
}