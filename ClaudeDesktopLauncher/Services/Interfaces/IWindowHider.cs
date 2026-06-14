namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Hides a Claude slot's main window to the system tray without
/// terminating the underlying process. Complements the existing
/// Kill/Close actions by offering a non-destructive "get out of my
/// way" for a slot the user wants to stop seeing but keep running.
///
/// <para>
/// Added in v1.10.5. Motivation: before this, the only way to hide
/// a slot was to use Claude's own close-to-tray (via the window's
/// minimise/close button), which requires bringing the slot to the
/// foreground first. A Hide button on the launcher's slot card means
/// the user can park a slot without context-switching to it.
/// </para>
///
/// <para>
/// The Show-from-tray side of the feature is intentionally NOT here
/// (deferred to v1.10.6). Showing a hidden window requires enumerating
/// all top-level windows (visible and hidden) to find the one
/// belonging to the target PID - <c>Process.MainWindowHandle</c>
/// returns <c>IntPtr.Zero</c> for hidden windows. Users can still
/// re-show a hidden slot via Claude's own system-tray icon in the
/// meantime; the existing tray-resident detection already surfaces
/// hidden slots in the launcher's TrayCard list, closing the loop.
/// </para>
/// </summary>
public interface IWindowHider
{
    /// <summary>
    /// Hides the main window of the process with the given
    /// <paramref name="processId"/>. Returns <c>true</c> on success,
    /// <c>false</c> if the process is gone, has no visible main
    /// window, or the Win32 call fails. Never throws - all failures
    /// are logged and collapsed to a boolean result so the UI stays
    /// responsive even when something goes wrong at the OS layer.
    /// </summary>
    bool TryHide(int processId);
}
