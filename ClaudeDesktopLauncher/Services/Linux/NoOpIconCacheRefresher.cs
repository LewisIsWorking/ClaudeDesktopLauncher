using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IIconCacheRefresher: does nothing. The Windows
/// implementation calls ie4uinit.exe -show to force Explorer to
/// reload icon cache after a Start Menu shortcut is healed. Linux
/// desktop environments handle .desktop file changes via inotify
/// automatically, so no explicit refresh is needed - this stub
/// satisfies the DI contract without any work.
/// </summary>
public class NoOpIconCacheRefresher : IIconCacheRefresher
{
    public void RefreshShortcutIcons() { }
}
