using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Persistent cache of a known-good Claude login state (Cookies + Local State
/// + Preferences) captured from a launcher-managed slot when it was cleanly
/// closed. Used to seed new slots so they come up already logged in, even
/// when no other Claude process is currently running from which to copy.
///
/// Cache files live at
/// <c>%APPDATA%\ClaudeDesktopLauncher\seed\</c>
/// and persist across launcher restarts.
///
/// Both <see cref="TrySnapshot"/> and <see cref="TrySeed"/> are tolerant of
/// failure: they return a bool rather than throwing, and leave the cache in
/// a consistent state (either fully updated or fully unchanged) so a partial
/// failure can never produce a corrupt cache that breaks future seeding.
/// </summary>
public interface ISlotSeedCache
{
    /// <summary>
    /// True when the cache contains a complete, well-formed snapshot
    /// (Cookies passing SQLite header check, Local State containing a
    /// non-empty os_crypt.encrypted_key).
    /// </summary>
    bool IsPopulated { get; }

    /// <summary>
    /// Copies the seed files (Cookies + Local State + Preferences) from
    /// <paramref name="source"/>'s data directory into the cache, overwriting
    /// any previous snapshot. Returns true only if every required file was
    /// copied and validated. On failure the previous cache contents (if any)
    /// are preserved.
    /// </summary>
    bool TrySnapshot(LaunchSlot source);

    /// <summary>
    /// Seeds <paramref name="target"/>'s data directory from the cache.
    /// Copies Cookies + Local State + Preferences. Returns true only if the
    /// cache was populated and every file was successfully written.
    /// </summary>
    bool TrySeed(LaunchSlot target);
}