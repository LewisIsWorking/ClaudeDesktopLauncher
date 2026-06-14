using System.Text;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Seeds a Claude slot with login credentials on first use.
///
/// Seeding order, each step attempted only if earlier steps fail:
/// 1. <see cref="ISlotSeedCache"/> - a launcher-maintained snapshot of
///    (Cookies + Local State + Preferences) from a previously-closed slot.
///    This is the most reliable source: the files are always unlocked and
///    the Local State contains the matching Chromium encryption key for the
///    Cookies. Applied on EVERY launch (not just first use) so that any
///    slot with stale or mismatched Cookies/Local State files is corrected.
/// 2. The default Claude profile at %APPDATA%\Claude (Cookies only).
/// 3. Any other already-seeded slot's Cookies.
///
/// Options 2 and 3 copy only Cookies. Those paths rely on the new slot
/// generating its own Local State on first launch; the encryption key will
/// therefore not match the copied cookies and login will usually fail. They
/// remain as a last resort for when the seed cache has never been populated
/// (e.g. a fresh install of the launcher, before any slot has been closed).
///
/// Chromium keeps the cookies SQLite file open while Claude is running, so copies
/// are attempted with <see cref="IFileSystem.CopyFileSharedRead"/> (FileShare.ReadWrite).
/// A mid-flush copy can still be corrupt; we verify the result starts with the
/// SQLite magic header ("SQLite format 3\0") and discard it otherwise so a bad
/// copy never prevents a later attempt from succeeding.
///
/// Seeding decisions are logged so login-persistence failures can be traced.
/// </summary>
public class SlotInitialiser : ISlotInitialiser
{
    /// <summary>
    /// A fresh Chromium cookies SQLite database is 20480 bytes.
    /// Anything above this threshold means real login cookies are stored.
    /// </summary>
    private const long MinimalCookiesSizeBytes = 20480;
    private const int MaxFallbackSlots = 10;

    /// <summary>
    /// SQLite 3 database file magic header. Every valid SQLite DB begins with
    /// exactly these 16 bytes. Used to reject partial/corrupt copies.
    /// See https://www.sqlite.org/fileformat.html#magic_header_string
    /// </summary>
    private static readonly byte[] SqliteMagic =
        Encoding.ASCII.GetBytes("SQLite format 3\0");

    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;
    private readonly ISlotSeedCache _seedCache;

    private static readonly string DefaultCookiesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Claude", "Network", "Cookies");

    public SlotInitialiser(
        IFileSystem fileSystem,
        ILoggingService logger,
        ISlotSeedCache seedCache)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _seedCache = seedCache;
    }

    public bool IsSeeded(LaunchSlot slot)
    {
        var cookiesPath = GetSlotCookiesPath(slot);
        if (!_fileSystem.FileExists(cookiesPath)) return false;
        return _fileSystem.GetFileSize(cookiesPath) > MinimalCookiesSizeBytes;
    }

    public void EnsureInitialised(LaunchSlot slot)
    {
        // Seed cache takes priority over any existing slot state. It is the
        // most authoritative source: a complete (Cookies + Local State +
        // Preferences) snapshot captured at the same point in time from a
        // cleanly-closed slot, verified for SQLite integrity and a valid
        // os_crypt.encrypted_key at capture.
        //
        // Re-seeding on every launch is deliberate and safe:
        //   - idempotent: an identical set of files produces identical state
        //   - cheap: ~15 ms of IO for three small files (< 30 KB total)
        //   - defensive: it corrects any slot whose Cookies/Local State pair
        //     has drifted out of sync - for example from manual user
        //     intervention, a prior launch under an incomplete seeding path,
        //     or cross-session leftovers. A mismatched pair causes Chromium
        //     to fail cookie decryption and show the login wall, which is
        //     exactly the user-visible bug this exists to prevent.
        //
        // TrySeed silently returns false if the cache is unpopulated or if
        // any destination file is currently locked (the slot is running),
        // which is exactly when skipping is safe.
        if (_seedCache.TrySeed(slot))
        {
            _logger.LogInfo($"Slot {slot.SlotNumber} seeded from seed cache");
            return;
        }

        // Seed cache unusable (empty or destination locked). Fall back to
        // legacy paths if the slot has nothing usable yet.
        if (IsSeeded(slot))
        {
            _logger.LogDebug(
                $"Slot {slot.SlotNumber} already seeded (seed cache unavailable)");
            return;
        }

        _logger.LogInfo($"Seeding slot {slot.SlotNumber}");

        var networkDir = GetSlotNetworkDir(slot);
        _fileSystem.CreateDirectory(networkDir);
        var destination = GetSlotCookiesPath(slot);

        if (TryCopyAndVerify(DefaultCookiesPath, destination))
        {
            _logger.LogInfo(
                $"Slot {slot.SlotNumber} seeded from default Claude profile (Cookies only)");
            return;
        }

        for (var i = 1; i <= MaxFallbackSlots; i++)
        {
            if (i == slot.SlotNumber) continue;
            var fallbackSlot = new LaunchSlot(i);
            if (!IsSeeded(fallbackSlot)) continue;
            if (TryCopyAndVerify(GetSlotCookiesPath(fallbackSlot), destination))
            {
                _logger.LogInfo(
                    $"Slot {slot.SlotNumber} seeded from fallback slot {i} (Cookies only)");
                return;
            }
        }

        _logger.LogWarning(
            $"Slot {slot.SlotNumber} could not be seeded - no valid source available");
    }

    /// <summary>
    /// Attempts to copy <paramref name="sourcePath"/> to <paramref name="destinationPath"/>
    /// using shared-read so locked source files can still be copied. Verifies the
    /// copy is a well-formed SQLite DB (correct magic header) before accepting it;
    /// removes the destination if verification fails so the next fallback is tried
    /// without a corrupt file left behind.
    /// </summary>
    private bool TryCopyAndVerify(string sourcePath, string destinationPath)
    {
        if (!_fileSystem.FileExists(sourcePath)) return false;
        try
        {
            _fileSystem.CopyFileSharedRead(sourcePath, destinationPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Copy from {sourcePath} failed: {ex.Message}");
            return false;
        }

        if (IsValidSqliteFile(destinationPath)) return true;

        _logger.LogWarning(
            $"Copy from {sourcePath} produced an invalid SQLite file - discarding");
        try
        {
            _fileSystem.DeleteFile(destinationPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                $"Could not delete invalid copy at {destinationPath}: {ex.Message}");
        }
        return false;
    }

    private bool IsValidSqliteFile(string path)
    {
        var header = _fileSystem.ReadFileHeader(path, SqliteMagic.Length);
        if (header.Length != SqliteMagic.Length) return false;
        for (var i = 0; i < SqliteMagic.Length; i++)
            if (header[i] != SqliteMagic[i]) return false;
        return true;
    }

    private static string GetSlotNetworkDir(LaunchSlot slot) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName, "Network");

    private static string GetSlotCookiesPath(LaunchSlot slot) =>
        Path.Combine(GetSlotNetworkDir(slot), "Cookies");
}