using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Resolves the Claude Desktop launcher script on Linux. The Debian
/// build of claude-desktop-debian (community port) installs a
/// launcher shell script at /usr/bin/claude-desktop which sets up
/// Wayland/Electron environment then invokes the bundled Electron
/// binary at /usr/lib/claude-desktop/node_modules/electron/dist/electron
/// with /usr/lib/claude-desktop/node_modules/electron/dist/resources/app.asar
/// passing "$@" through, so the launcher's --user-data-dir=... arg
/// works the same way as on Windows.
///
/// <para>v1.10.19 MVP: hard-codes the canonical path. A more thorough
/// resolver could check $PATH and inspect alternative install
/// locations (snap, flatpak, AppImage), but that is deferred to a
/// later milestone once the basic Linux launch path is proven.</para>
/// </summary>
public class LinuxClaudePathResolver : IClaudePathResolver
{
    private const string CanonicalPath = "/usr/bin/claude-desktop";

    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;

    public LinuxClaudePathResolver(IFileSystem fileSystem, ILoggingService logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string? ResolveClaudeExePath()
    {
        _logger.LogInfo("Resolving Claude executable path (Linux)");
        if (_fileSystem.FileExists(CanonicalPath))
        {
            _logger.LogInfo($"Resolved directly: {CanonicalPath}");
            return CanonicalPath;
        }
        _logger.LogWarning(
            $"Claude executable not found at {CanonicalPath}. Install claude-desktop-debian or symlink the binary.");
        return null;
    }

    public bool IsClaudeInstalled() => ResolveClaudeExePath() is not null;
}
