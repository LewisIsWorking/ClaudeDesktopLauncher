using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
namespace ClaudeDesktopLauncher.Services;
/// <summary>
/// Win32 implementation of <see cref="IWindowShower"/>. Enumerates all
/// top-level windows via <c>EnumWindows</c>, matches the target PID via
/// <c>GetWindowThreadProcessId</c>, selects the best candidate window,
/// then calls <c>ShowWindow(hwnd, SW_SHOW)</c> and
/// <c>SetForegroundWindow(hwnd)</c>.
///
/// <para>
/// Window selection heuristic (in priority order):
/// <list type="number">
///   <item>Skip windows with <c>WS_EX_TOOLWINDOW</c> extended style -
///     these are Electron renderer helper/popup windows that should not
///     appear in task-switchers and are not the main browser window.</item>
///   <item>Among remaining matches, prefer the window with a non-empty
///     title - the main Electron window always has a page title.</item>
///   <item>Fall back to the first remaining match if no titled window
///     is found.</item>
/// </list>
/// </para>
///
/// <para>
/// The managed delegate passed to <c>EnumWindows</c> is held in a local
/// variable for the duration of the call to prevent GC collection during
/// enumeration - a known pitfall when pinning is omitted.
/// </para>
///
/// <para>
/// <c>SetForegroundWindow</c> can fail silently when Windows'
/// foreground-stealing prevention is active. This is an accepted OS
/// quirk for v1.10.6; we log the outcome but do not fight it.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class Win32WindowShower : IWindowShower
{
    private const int SW_SHOW = 5;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    private readonly ILoggingService _logger;
    public Win32WindowShower(ILoggingService logger)
    {
        _logger = logger;
    }
    public bool TryShow(int processId)
    {
        try
        {
            var hwnd = FindBestWindow((uint)processId);
            if (hwnd == IntPtr.Zero)
            {
                _logger.LogWarning(
                    $"TryShow({processId}): no matching top-level window found " +
                    $"(process may be gone or has no enumerable window).");
                return false;
            }
            ShowWindow(hwnd, SW_SHOW);
            var fg = SetForegroundWindow(hwnd);
            _logger.LogInfo(
                $"TryShow({processId}): hwnd=0x{hwnd:X}, " +
                $"SetForegroundWindow={fg}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"TryShow({processId}) crashed: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// Enumerates all top-level windows and returns the best candidate
    /// hwnd for the given PID, or <see cref="IntPtr.Zero"/> if none found.
    /// </summary>
    private IntPtr FindBestWindow(uint targetPid)
    {
        IntPtr firstMatch = IntPtr.Zero;
        IntPtr titledMatch = IntPtr.Zero;
        // Keep a local reference so the GC does not collect the delegate
        // while EnumWindows is still calling back into managed code.
        EnumWindowsProc callback = (hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != targetPid) return true;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
            if (firstMatch == IntPtr.Zero)
                firstMatch = hwnd;
            if (titledMatch == IntPtr.Zero && GetWindowTitle(hwnd).Length > 0)
                titledMatch = hwnd;
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);
        return titledMatch != IntPtr.Zero ? titledMatch : firstMatch;
    }
    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}