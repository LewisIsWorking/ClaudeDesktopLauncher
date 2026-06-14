namespace ClaudeDesktopLauncher.Services.Interfaces;
/// <summary>
/// Restores a hidden Claude slot's window to the foreground. The
/// other half of <see cref="IWindowHider"/>: where Hide uses
/// <c>Process.MainWindowHandle</c> (works only for visible windows),
/// Show must enumerate all top-level windows via <c>EnumWindows</c>
/// and match by PID because <c>MainWindowHandle</c> returns
/// <c>IntPtr.Zero</c> for hidden windows.
///
/// <para>
/// Added in v1.10.6. Kept as a separate interface from
/// <see cref="IWindowHider"/> for single-responsibility: the two
/// operations have different Win32 footprints, different failure modes,
/// and benefit from independent testability and logging.
/// </para>
///
/// <para>
/// <c>SetForegroundWindow</c> can fail silently if Windows'
/// foreground-lock mechanism is active (the app does not own the
/// foreground token). This is a documented OS quirk; implementations
/// must log the outcome but must not throw.
/// </para>
/// </summary>
public interface IWindowShower
{
    /// <summary>
    /// Finds the main window belonging to <paramref name="processId"/>
    /// by enumerating all top-level windows, then calls
    /// <c>ShowWindow(hwnd, SW_SHOW)</c> and
    /// <c>SetForegroundWindow(hwnd)</c> to bring it back to the screen.
    /// Returns <c>true</c> on success, <c>false</c> if no matching
    /// window is found, the process is gone, or the Win32 calls fail.
    /// Never throws - all failures are logged and collapsed to a
    /// boolean so the UI stays responsive even if something goes wrong
    /// at the OS layer.
    /// </summary>
    bool TryShow(int processId);
}