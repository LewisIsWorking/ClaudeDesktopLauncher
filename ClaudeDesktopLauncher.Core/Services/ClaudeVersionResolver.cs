using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Reads the Claude Desktop ProductVersion from the resolved claude.exe
/// via <see cref="IFileSystem.GetFileProductVersion"/>.
///
/// Depends on <see cref="IClaudePathCache"/> rather than
/// <see cref="IClaudePathResolver"/> so the path is resolved only once at
/// application startup; subsequent calls are cache-hits. This avoids the
/// PowerShell AppxPackage query that the direct resolver can fall through
/// to, which adds ~2 s each call on a cold runner.
///
/// Returns <c>null</c> silently when Claude is not installed or the exe
/// has no readable version resource - the caller typically displays the
/// launcher version alone in that case.
/// </summary>
public class ClaudeVersionResolver : IClaudeVersionResolver
{
    private readonly IClaudePathCache _pathCache;
    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;

    public ClaudeVersionResolver(
        IClaudePathCache pathCache,
        IFileSystem fileSystem,
        ILoggingService logger)
    {
        _pathCache = pathCache;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string? GetClaudeVersion()
    {
        var path = _pathCache.GetPath();
        if (path is null)
        {
            _logger.LogDebug("Cannot read Claude version: exe path unresolved");
            return null;
        }

        var version = _fileSystem.GetFileProductVersion(path);
        if (version is null)
        {
            _logger.LogWarning(
                $"Claude exe found at {path} but ProductVersion was unreadable");
            return null;
        }

        _logger.LogInfo($"Resolved Claude Desktop version: {version}");
        return version;
    }
}