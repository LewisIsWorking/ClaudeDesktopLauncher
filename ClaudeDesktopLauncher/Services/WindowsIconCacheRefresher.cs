using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Windows implementation of <see cref="IIconCacheRefresher"/> that
/// invokes <c>ie4uinit.exe -show</c> to force Explorer to reload its
/// icon cache. This is Microsoft's documented method for refreshing
/// shortcut icons after a .lnk target or icon location changes
/// without requiring a reboot or explorer.exe restart.
///
/// <para>
/// <c>ie4uinit.exe</c> ships with Windows and lives in
/// <c>%WINDIR%\System32\</c>, so we can invoke it by short name -
/// PATH resolution finds it reliably on every supported Windows
/// version. The <c>-show</c> flag is a no-op on modern Windows that
/// nevertheless triggers the cache flush (historical Microsoft quirk,
/// preserved for backwards compatibility).
/// </para>
///
/// <para>
/// Delegates the process launch to <see cref="IProcessService"/> so
/// tests can verify the correct invocation happened without actually
/// spawning a process.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsIconCacheRefresher : IIconCacheRefresher
{
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;

    public WindowsIconCacheRefresher(
        IProcessService processService, ILoggingService logger)
    {
        _processService = processService;
        _logger = logger;
    }

    public void RefreshShortcutIcons()
    {
        try
        {
            _processService.Start("ie4uinit.exe", "-show", useShellExecute: false);
            _logger.LogInfo("Requested Windows icon-cache refresh via ie4uinit.exe -show");
        }
        catch (Exception ex)
        {
            // Non-fatal - the icon will eventually refresh on its own
            // (logoff/reboot, or natural cache expiry). Log and move on.
            _logger.LogWarning($"Icon cache refresh failed: {ex.Message}");
        }
    }
}
