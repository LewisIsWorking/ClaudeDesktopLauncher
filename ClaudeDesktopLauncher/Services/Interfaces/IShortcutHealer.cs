namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Self-heal missing Start Menu (or Desktop) shortcuts on startup.
///
/// <para>
/// Added in v1.10.2 after the v1.10.0 -> v1.10.1 auto-update on one
/// real-world machine silently lost the Start Menu shortcut. Velopack
/// 0.0.1298's "update existing shortcut" code path logged success but
/// left the shortcut's parent folder empty, making the app
/// unfindable via Windows Search. Full incident writeup lives in
/// <c>docs/dev/VELOPACK.md</c> under
/// "Upstream bug: Start Menu shortcut may vanish during update apply".
/// </para>
///
/// <para>
/// Rather than wait for an upstream Velopack fix (which might take
/// weeks to release and re-reach end users), the launcher heals
/// itself on every startup. If the expected shortcut is present, no
/// action; if missing, recreate it via the WScript.Shell COM API
/// that Velopack itself uses. This means: users never see the bug
/// even if it reoccurs on a future Velopack-managed update.
/// </para>
///
/// <para>
/// Dev-build guard: on a <c>dotnet run</c> development build the
/// launcher is not installed to <c>%LOCALAPPDATA%</c> and therefore
/// has no "expected" shortcut. <see cref="HealIfMissing"/> returns
/// <see cref="ShortcutHealResult.SkippedDevBuild"/> without touching
/// the file system so development never accidentally sprays
/// shortcuts pointing at <c>bin\Debug\</c> paths.
/// </para>
/// </summary>
public interface IShortcutHealer
{
    /// <summary>
    /// Checks whether the expected Start Menu shortcut exists and
    /// recreates it if missing. Safe to call on every startup.
    /// Never throws - all failures collapse to
    /// <see cref="ShortcutHealResult.Failed"/> so a broken shortcut
    /// probe can never block launcher startup.
    /// </summary>
    ShortcutHealResult HealIfMissing();
}

/// <summary>
/// Outcome of a <see cref="IShortcutHealer.HealIfMissing"/> call.
/// Exposed for logging/diagnostics and for tests to assert the
/// healer made the right branch decision for a given input state.
/// </summary>
public enum ShortcutHealResult
{
    /// <summary>
    /// The launcher is a dev build (not installed via Velopack).
    /// No shortcut should exist in <c>%APPDATA%\Microsoft\Windows\Start Menu</c>
    /// and the healer did not attempt to create one.
    /// </summary>
    SkippedDevBuild,

    /// <summary>
    /// The shortcut was already present. No action taken.
    /// </summary>
    AlreadyPresent,

    /// <summary>
    /// The shortcut was missing but has been recreated successfully.
    /// </summary>
    HealedMissing,

    /// <summary>
    /// The shortcut was missing and recreation failed. The launcher
    /// remains usable; users can launch it via Desktop shortcut or
    /// by running the exe directly.
    /// </summary>
    Failed
}
