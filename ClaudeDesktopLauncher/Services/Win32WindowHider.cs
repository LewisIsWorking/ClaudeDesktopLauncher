using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Win32 implementation of <see cref="IWindowHider"/> using
/// <c>Process.MainWindowHandle</c> to resolve the target window and
/// <c>ShowWindow(hwnd, SW_HIDE)</c> to hide it. Claude's Electron
/// main window survives being hidden (it's the same mechanism
/// Electron uses for its own close-to-tray behaviour) so the process
/// keeps running, MCP connections stay alive, and the slot will
/// surface in the launcher's TrayCard list on the next scanner poll.
///
/// <para>
/// Does NOT call <c>ShowWindow</c> with <c>SW_MINIMIZE</c> - that
/// leaves the window in the taskbar, which defeats the purpose.
/// <c>SW_HIDE</c> removes it from the taskbar entirely, matching
/// the user's mental model of "close to tray".
/// </para>
///
/// <para>
/// Tray-resident Claude windows register themselves as visible only
/// from the taskbar's perspective via a separate Electron/Windows
/// tray-icon API; CoODL's scanner already handles the visibility
/// classification via <see cref="ClaudeProcessMainIdentifier"/> and
/// surfaces hidden slots in the UI's TrayCard list, so no
/// coordination is needed here - just hide the window and trust the
/// existing pipeline.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class Win32WindowHider : IWindowHider
{
    private const int SW_HIDE = 0;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly ILoggingService _logger;

    public Win32WindowHider(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool TryHide(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            var hwnd = proc.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                _logger.LogWarning(
                    $"TryHide({processId}): MainWindowHandle is zero " +
                    $"(process may already be tray-resident or is a " +
                    $"child process without a top-level window).");
                return false;
            }

            var ok = ShowWindow(hwnd, SW_HIDE);
            if (!ok)
            {
                _logger.LogWarning(
                    $"TryHide({processId}): ShowWindow returned false. " +
                    $"Window may already be hidden.");
                return false;
            }

            _logger.LogInfo($"TryHide({processId}): window hidden to tray");
            return true;
        }
        catch (ArgumentException)
        {
            // Process no longer exists - user killed it between UI
            // render and button click. Not an error we care about.
            _logger.LogInfo(
                $"TryHide({processId}): process no longer exists");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"TryHide({processId}) crashed: {ex.Message}");
            return false;
        }
    }
}
