using System.Text;
using System.Text.Json;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Pure validation helpers for the slot seed cache. Extracted from
/// <see cref="FileSlotSeedCache"/> so the big cache class stays below
/// the 200-line per-file limit and so these checks can be reasoned
/// about independently (they have no side effects beyond reading the
/// supplied file).
///
/// <para>
/// <see cref="IsValidSqliteFile"/> confirms a file starts with the
/// standard SQLite 3 magic header (<c>"SQLite format 3\0"</c>, 16
/// bytes). Chromium's Cookies DB is SQLite, so this detects the
/// "file-locked-during-copy" corruption case where we land a partial
/// or empty blob.
/// </para>
///
/// <para>
/// <see cref="HasEncryptedKey"/> confirms a Chromium Local State JSON
/// blob has a non-empty <c>os_crypt.encrypted_key</c> - the encryption
/// key for cookies. Without it, seeded cookies can't be decrypted and
/// the user hits an unexpected login wall.
/// </para>
/// </summary>
internal static class SeedCacheValidators
{
    /// <summary>The 16-byte SQLite 3 magic header.</summary>
    public static readonly byte[] SqliteMagic =
        Encoding.ASCII.GetBytes("SQLite format 3\0");

    /// <summary>
    /// Reads the first 16 bytes of <paramref name="path"/> through
    /// <paramref name="fileSystem"/> and returns true iff they match
    /// the SQLite 3 magic header byte-for-byte.
    /// </summary>
    public static bool IsValidSqliteFile(IFileSystem fileSystem, string path)
    {
        var header = fileSystem.ReadFileHeader(path, SqliteMagic.Length);
        if (header.Length != SqliteMagic.Length) return false;
        for (var i = 0; i < SqliteMagic.Length; i++)
            if (header[i] != SqliteMagic[i]) return false;
        return true;
    }

    /// <summary>
    /// Parses the Chromium Local State JSON at
    /// <paramref name="localStatePath"/> and returns true iff
    /// <c>os_crypt.encrypted_key</c> is a non-empty string. IO or JSON
    /// errors are swallowed - a missing/unreadable file is a failed
    /// validation, not a crash.
    /// </summary>
    public static bool HasEncryptedKey(IFileSystem fileSystem, string localStatePath)
    {
        try
        {
            var json = fileSystem.ReadAllText(localStatePath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt)) return false;
            if (!osCrypt.TryGetProperty("encrypted_key", out var key)) return false;
            return key.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(key.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
