namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Refreshes Windows' icon cache so newly-created or updated shortcuts
/// display their correct icon without requiring an explorer.exe restart
/// or a reboot.
///
/// <para>
/// Added in v1.10.3 after observing on 2026-04-20 that when
/// <see cref="IShortcutHealer"/> recreated a missing Start Menu .lnk
/// on Lewis's machine, Windows Search still displayed the generic
/// document icon for minutes afterwards because the shell's icon
/// cache retained the previous broken state. The fix documented by
/// Microsoft is <c>ie4uinit.exe -show</c> (a utility shipped with
/// Windows that forces Explorer to reload icons without interrupting
/// the user). Wrapped behind an interface so
/// <see cref="IShortcutHealer"/> can be unit-tested without actually
/// spawning the process.
/// </para>
///
/// <para>
/// Only called on the <see cref="ShortcutHealResult.HealedMissing"/>
/// branch of the healer - <see cref="ShortcutHealResult.AlreadyPresent"/>
/// doesn't need a refresh (nothing changed on disk) and the refresh
/// is cheap but not free (tiny process spawn), so firing it
/// unnecessarily on every launcher startup would be wasteful.
/// </para>
/// </summary>
public interface IIconCacheRefresher
{
    /// <summary>
    /// Fire-and-forget request for Windows to reload its icon cache.
    /// Must not throw; any failure is logged internally. Returns
    /// immediately - the actual refresh happens asynchronously in
    /// Explorer and may take a second or two to visibly propagate.
    /// </summary>
    void RefreshShortcutIcons();
}
