using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// File-backed implementation of <see cref="ISlotSeedCache"/>.
///
/// Cache layout:
/// <code>
/// %APPDATA%\ClaudeDesktopLauncher\seed\
///     Cookies            (SQLite DB from source slot's Network\Cookies)
///     LocalState         (JSON from source slot's Local State)
///     Preferences        (JSON from source slot's Preferences)
/// </code>
///
/// The filename "LocalState" is used (without the space) because we treat it
/// as an internal artefact; when we write it back to a target slot we use
/// Chromium's expected name "Local State".
///
/// Pure validation logic (SQLite header check, Local State encrypted-key
/// presence check) lives in <see cref="SeedCacheValidators"/> so this
/// file stays focused on the IO-orchestration concern.
/// </summary>
public class FileSlotSeedCache : ISlotSeedCache
{
    internal const string CookiesFileName = "Cookies";
    internal const string LocalStateFileName = "LocalState";
    internal const string PreferencesFileName = "Preferences";

    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;
    private readonly string _cacheDirectory;

    public FileSlotSeedCache(IFileSystem fileSystem, ILoggingService logger)
        : this(fileSystem, logger, DefaultCacheDirectory()) { }

    public FileSlotSeedCache(IFileSystem fileSystem, ILoggingService logger, string cacheDirectory)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _cacheDirectory = cacheDirectory;
        EnsureDirectoryExists();
    }

    public bool IsPopulated =>
        _fileSystem.FileExists(Path.Combine(_cacheDirectory, CookiesFileName)) &&
        _fileSystem.FileExists(Path.Combine(_cacheDirectory, LocalStateFileName)) &&
        _fileSystem.FileExists(Path.Combine(_cacheDirectory, PreferencesFileName)) &&
        SeedCacheValidators.IsValidSqliteFile(_fileSystem, Path.Combine(_cacheDirectory, CookiesFileName)) &&
        SeedCacheValidators.HasEncryptedKey(_fileSystem, Path.Combine(_cacheDirectory, LocalStateFileName));

    public bool TrySnapshot(LaunchSlot source)
    {
        var srcCookies = GetSourceCookiesPath(source);
        var srcLocalState = GetSourceLocalStatePath(source);
        var srcPreferences = GetSourcePreferencesPath(source);

        var dstCookies = Path.Combine(_cacheDirectory, CookiesFileName);
        var dstLocalState = Path.Combine(_cacheDirectory, LocalStateFileName);
        var dstPreferences = Path.Combine(_cacheDirectory, PreferencesFileName);

        _logger.LogInfo($"Snapshotting slot {source.SlotNumber} into seed cache");

        if (!TryCopySharedRead(srcCookies, dstCookies)) return false;
        if (!SeedCacheValidators.IsValidSqliteFile(_fileSystem, dstCookies))
        {
            _logger.LogWarning("Snapshot cookies failed SQLite header check - discarding");
            TryDelete(dstCookies);
            return false;
        }

        if (!TryCopySharedRead(srcLocalState, dstLocalState))
        {
            TryDelete(dstCookies);
            return false;
        }
        if (!SeedCacheValidators.HasEncryptedKey(_fileSystem, dstLocalState))
        {
            _logger.LogWarning("Snapshot Local State missing os_crypt.encrypted_key - discarding");
            TryDelete(dstCookies);
            TryDelete(dstLocalState);
            return false;
        }

        if (!TryCopySharedRead(srcPreferences, dstPreferences))
        {
            TryDelete(dstCookies);
            TryDelete(dstLocalState);
            return false;
        }

        _logger.LogInfo($"Seed cache updated from slot {source.SlotNumber}");
        return true;
    }

    public bool TrySeed(LaunchSlot target)
    {
        if (!IsPopulated)
        {
            _logger.LogDebug("Seed cache not populated - cannot seed from cache");
            return false;
        }

        var targetDir = GetTargetDataDirectory(target);
        var targetNetworkDir = Path.Combine(targetDir, "Network");
        _fileSystem.CreateDirectory(targetNetworkDir);

        try
        {
            _fileSystem.CopyFile(
                Path.Combine(_cacheDirectory, CookiesFileName),
                Path.Combine(targetNetworkDir, "Cookies"));
            _fileSystem.CopyFile(
                Path.Combine(_cacheDirectory, LocalStateFileName),
                Path.Combine(targetDir, "Local State"));
            _fileSystem.CopyFile(
                Path.Combine(_cacheDirectory, PreferencesFileName),
                Path.Combine(targetDir, "Preferences"));
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Seed cache write to slot {target.SlotNumber} failed: {ex.Message}");
            return false;
        }

        _logger.LogInfo($"Slot {target.SlotNumber} seeded from seed cache");
        return true;
    }

    private bool TryCopySharedRead(string source, string destination)
    {
        if (!_fileSystem.FileExists(source))
        {
            _logger.LogWarning($"Snapshot source missing: {source}");
            return false;
        }
        try
        {
            _fileSystem.CopyFileSharedRead(source, destination);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Snapshot copy from {source} failed: {ex.Message}");
            return false;
        }
    }

    private void TryDelete(string path)
    {
        try { _fileSystem.DeleteFile(path); }
        catch (IOException ex) { _logger.LogWarning($"Cleanup delete of {path} failed: {ex.Message}"); }
    }

    private void EnsureDirectoryExists()
    {
        try
        {
            if (!_fileSystem.DirectoryExists(_cacheDirectory))
                _fileSystem.CreateDirectory(_cacheDirectory);
        }
        catch (IOException ex)
        {
            _logger.LogWarning($"Seed cache directory creation failed: {ex.Message}");
        }
    }

    private static string GetSourceCookiesPath(LaunchSlot slot) =>
        Path.Combine(GetTargetDataDirectory(slot), "Network", "Cookies");

    private static string GetSourceLocalStatePath(LaunchSlot slot) =>
        Path.Combine(GetTargetDataDirectory(slot), "Local State");

    private static string GetSourcePreferencesPath(LaunchSlot slot) =>
        Path.Combine(GetTargetDataDirectory(slot), "Preferences");

    private static string GetTargetDataDirectory(LaunchSlot slot) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName);

    private static string DefaultCacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeDesktopLauncher",
            "seed");
}
