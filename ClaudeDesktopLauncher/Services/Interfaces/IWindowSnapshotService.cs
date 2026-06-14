using Avalonia;

namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Captures the rendered content of an Avalonia <see cref="Visual"/> (typically
/// the main window) and puts it on the system clipboard as a PNG image.
/// Used by the "Copy screenshot" button so users can paste directly into Slack,
/// Discord, Teams, Word, etc. without round-tripping through a file explorer.
///
/// Always fails gracefully (never throws, never crashes the app): a denied
/// clipboard, a closed window, or a visual that rendered to zero pixels
/// simply returns <c>false</c>.
/// </summary>
public interface IWindowSnapshotService
{
    /// <summary>
    /// Renders <paramref name="visual"/> to a PNG and places it on the
    /// clipboard. Returns <c>true</c> when the clipboard was populated,
    /// <c>false</c> for any expected failure (zero-sized visual, clipboard
    /// unavailable, set-data exception).
    /// </summary>
    Task<bool> CaptureAndCopyAsync(Visual visual);
}