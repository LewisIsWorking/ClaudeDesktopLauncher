using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Computes the combined on-disk size of all Claude data directories
/// under <c>%LOCALAPPDATA%</c> by walking the file tree on a
/// thread-pool thread.
///
/// <para>
/// Scans two naming patterns:
/// <list type="bullet">
///   <item><c>ClaudeSlot*</c> — current naming scheme</item>
///   <item><c>ClaudeInstance*</c> — legacy naming scheme from older
///   launcher versions (directories are real Chromium profiles even
///   though the launcher no longer creates them)</item>
/// </list>
/// Each directory stores a full Chromium/Electron profile: IndexedDB,
/// Cache, GPU cache, extensions, and session data.
/// </para>
///
/// <para>
/// The scan is slow on large installs so this service is called once
/// at startup and on demand — never on the main refresh tick.
/// </para>
/// </summary>
public class ClaudeDiskUsageService : IClaudeDiskUsageService
{
    /// <summary>
    /// Directory name patterns that identify Claude data directories.
    /// Both are top-level children of <c>%LOCALAPPDATA%</c>.
    /// </summary>
    private static readonly string[] ScanPatterns = ["ClaudeSlot*", "ClaudeInstance*"];

    private readonly Func<string> _localAppDataResolver;

    /// <summary>Production constructor — resolves %LOCALAPPDATA% at call time.</summary>
    public ClaudeDiskUsageService()
        : this(() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) { }

    /// <summary>Testing seam — inject a custom root resolver.</summary>
    internal ClaudeDiskUsageService(Func<string> localAppDataResolver)
    {
        _localAppDataResolver = localAppDataResolver;
    }

    /// <inheritdoc/>
    public Task<double> GetTotalGbAsync() =>
        Task.Run(() =>
        {
            try
            {
                var root = _localAppDataResolver();
                var dirs = ScanPatterns
                    .SelectMany(p => Directory.GetDirectories(
                        root, p, SearchOption.TopDirectoryOnly))
                    .Distinct()
                    .ToList();

                if (dirs.Count == 0) return 0.0;

                long totalBytes = 0;
                foreach (var dir in dirs)
                {
                    foreach (var file in Directory.EnumerateFiles(
                        dir, "*", SearchOption.AllDirectories))
                    {
                        try { totalBytes += new FileInfo(file).Length; }
                        catch { /* file deleted mid-scan — skip */ }
                    }
                }
                return Math.Round(totalBytes / 1_073_741_824.0, 1);
            }
            catch
            {
                return 0.0;
            }
        });
}