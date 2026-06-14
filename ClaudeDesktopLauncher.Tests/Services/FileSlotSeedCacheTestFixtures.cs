using System.Text;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

internal static class FileSlotSeedCacheTestFixtures
{
    internal static readonly byte[] ValidSqliteHeader =
        Encoding.ASCII.GetBytes("SQLite format 3\0");
    internal static readonly byte[] InvalidHeader = new byte[16];

    internal const string ValidLocalStateJson =
        "{\"os_crypt\":{\"audit_enabled\":true,\"encrypted_key\":\"RFBBUEkAAA==\"}}";
    internal const string EmptyKeyLocalStateJson =
        "{\"os_crypt\":{\"audit_enabled\":true,\"encrypted_key\":\"\"}}";
    internal const string NoKeyLocalStateJson =
        "{\"os_crypt\":{\"audit_enabled\":true}}";
    internal const string MalformedJson = "{not valid json";

    internal static readonly string CacheDir =
        Path.Combine(Path.GetTempPath(), "coodl-seed-test");
    internal static string CacheCookies => Path.Combine(CacheDir, "Cookies");
    internal static string CacheLocalState => Path.Combine(CacheDir, "LocalState");
    internal static string CachePreferences => Path.Combine(CacheDir, "Preferences");

    internal static string SlotCookies => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeSlot1", "Network", "Cookies");
    internal static string SlotLocalState => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeSlot1", "Local State");
    internal static string SlotPreferences => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClaudeSlot1", "Preferences");

    internal static FileSlotSeedCache CreateSut(IFileSystem fs, ILoggingService logger) =>
        new(fs, logger, CacheDir);
}