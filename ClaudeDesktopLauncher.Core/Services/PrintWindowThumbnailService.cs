using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Win32 <see cref="IWindowThumbnailService"/> using <c>PrintWindow</c>
/// with <c>PW_RENDERFULLCONTENT=2</c>. This flag asks Windows to render
/// the window's full device context including hardware-accelerated
/// chrome (Chromium-rendered content in Claude's case). The alternative
/// <c>CopyFromScreen</c> would capture whatever is visible at the
/// screen coordinates, which breaks when another window is on top.
///
/// <para>
/// The service returns <c>null</c> (never throws) on all recoverable
/// failures: process gone, no main window handle (tray-resident), or
/// transient GDI pressure. Callers treat <c>null</c> as "preserve the
/// current thumbnail" rather than "clear" - that's how tray-resident
/// slots retain their last-known frame.
/// </para>
///
/// <para>
/// Following the pattern established by <c>SystemProcessService</c> and
/// <c>WmiClaudeProcessScanner</c>, this integration-heavy class is
/// unit-tested by proxy through <see cref="IWindowThumbnailService"/>
/// mocks used by higher-layer tests.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class PrintWindowThumbnailService : IWindowThumbnailService
{
    private const uint PW_RENDERFULLCONTENT = 0x2;

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int L, T, R, B; }

    private readonly ILoggingService _logger;

    public PrintWindowThumbnailService(ILoggingService logger)
    {
        _logger = logger;
    }

    public byte[]? CapturePngBytes(int processId, int width, int height)
    {
        var handle = TryResolveMainWindowHandle(processId);
        if (handle == IntPtr.Zero) return null;

        if (!GetClientRect(handle, out var rect)) return null;
        var srcWidth = rect.R - rect.L;
        var srcHeight = rect.B - rect.T;
        if (srcWidth <= 0 || srcHeight <= 0) return null;

        try
        {
            using var full = CaptureFullBitmap(handle, srcWidth, srcHeight);
            if (full is null) return null;
            using var thumb = ScaleToFit(full, width, height);
            return EncodePng(thumb);
        }
        catch (Exception ex)
        {
            // PrintWindow can fail under GDI resource pressure, display
            // driver reset, etc. Swallow and return null - the next poll
            // tick retries.
            _logger.LogWarning($"Thumbnail capture failed for PID {processId}: {ex.Message}");
            return null;
        }
    }

    private static IntPtr TryResolveMainWindowHandle(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            return proc.MainWindowHandle;
        }
        catch (ArgumentException)
        {
            return IntPtr.Zero;
        }
    }

    private static Bitmap? CaptureFullBitmap(IntPtr handle, int width, int height)
    {
        var bmp = new Bitmap(width, height);
        try
        {
            using var g = Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            var ok = PrintWindow(handle, hdc, PW_RENDERFULLCONTENT);
            g.ReleaseHdc(hdc);
            if (!ok) { bmp.Dispose(); return null; }
        }
        catch { bmp.Dispose(); throw; }
        return bmp;
    }

    /// <summary>
    /// Letterboxed resize: the source image is fit entirely inside the
    /// target box preserving aspect ratio. Avoids cropping so users see
    /// the whole window, at the cost of sometimes having empty bands
    /// when the source aspect differs from the target aspect. For
    /// Claude's roughly 3:2 window shape and our 240x150 target that's
    /// a near-perfect fit so bands are negligible.
    /// </summary>
    private static Bitmap ScaleToFit(Bitmap src, int boxW, int boxH)
    {
        var scale = Math.Min((double)boxW / src.Width, (double)boxH / src.Height);
        var w = Math.Max(1, (int)(src.Width * scale));
        var h = Math.Max(1, (int)(src.Height * scale));
        var dst = new Bitmap(w, h);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, w, h);
        return dst;
    }

    private static byte[] EncodePng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
