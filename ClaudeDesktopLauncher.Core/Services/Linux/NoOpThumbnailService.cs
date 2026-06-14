using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Linux stub IWindowThumbnailService: always returns null (= no
/// thumbnail available). On Wayland sessions, capturing another
/// application's window is fundamentally restricted by the compositor
/// for privacy reasons, so a real Linux thumbnail service would
/// require xdg-desktop-portal's ScreenCast interface plus user
/// consent - significantly more involved than the Win32 PrintWindow
/// path on Windows. Deferred to a future milestone; for v1.10.19 the
/// slot cards on Linux simply show no thumbnail.
/// </summary>
public class NoOpThumbnailService : IWindowThumbnailService
{
    public byte[]? CapturePngBytes(int processId, int width, int height) => null;
}
