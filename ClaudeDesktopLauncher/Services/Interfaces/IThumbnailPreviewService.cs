using Avalonia.Media.Imaging;

namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Opens a modal-ish Avalonia window showing a window thumbnail at a
/// large size. Lives in the UI project (not Core) because the contract
/// produces an Avalonia <see cref="Bitmap"/> internally.
///
/// <para>
/// v1.9.2 change: <see cref="Show"/> now captures a <b>fresh
/// high-resolution</b> snapshot (1920x1200 target) at preview time
/// rather than reusing the small cached card thumbnail. The old
/// approach upscaled a 240x150 bitmap to ~900x600 which looked
/// blurry; fresh capture gives a crisp near-native preview. The
/// cached thumbnail is passed as <paramref name="fallback"/> and only
/// used when fresh capture fails - for example, when the slot is
/// tray-resident and no main window handle is available.
/// </para>
///
/// <para>
/// Wrapped behind an interface so VM tests that exercise the
/// show-preview callback don't need a running Avalonia application.
/// </para>
/// </summary>
public interface IThumbnailPreviewService
{
    /// <summary>
    /// Opens the preview window. Tries a fresh high-resolution
    /// capture of <paramref name="processId"/>'s main window first;
    /// falls back to <paramref name="fallback"/> if fresh capture
    /// returns null (window gone to tray, PID exited, GDI pressure).
    /// If both are unavailable the call is a silent no-op so callers
    /// never need to guard themselves. <paramref name="title"/> is
    /// shown in the preview window's title bar so multi-slot users
    /// can tell which window they're looking at.
    /// </summary>
    void Show(int processId, Bitmap? fallback, string title);
}
