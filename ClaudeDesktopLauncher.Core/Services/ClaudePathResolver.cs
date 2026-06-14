using System.Diagnostics;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Resolves the Claude Desktop MSIX executable path on Windows.
/// Tries direct filesystem access first, falls back to PowerShell query.
/// Each resolution strategy is logged so failures can be diagnosed.
/// </summary>
public class ClaudePathResolver : IClaudePathResolver
{
    private const string WindowsAppsPath = @"C:\Program Files\WindowsApps";
    private const string ClaudeExeRelativePath = @"app\claude.exe";
    private const string ClaudePackagePattern = "Claude_*";

    private readonly IFileSystem _fileSystem;
    private readonly ILoggingService _logger;

    public ClaudePathResolver(IFileSystem fileSystem, ILoggingService logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string? ResolveClaudeExePath()
    {
        _logger.LogInfo("Resolving Claude executable path");
        var direct = TryResolveDirectly();
        if (direct is not null)
        {
            _logger.LogInfo($"Resolved directly: {direct}");
            return direct;
        }

        var viaPs = ResolveViaPowerShell();
        if (viaPs is not null)
        {
            _logger.LogInfo($"Resolved via PowerShell: {viaPs}");
            return viaPs;
        }

        _logger.LogWarning("Claude executable could not be resolved by any strategy");
        return null;
    }

    public bool IsClaudeInstalled() => ResolveClaudeExePath() is not null;

    private string? TryResolveDirectly()
    {
        try
        {
            var dirs = _fileSystem.GetDirectories(WindowsAppsPath, ClaudePackagePattern);
            _logger.LogDebug($"Direct strategy found {dirs.Length} matching package directories");
            return dirs
                .Select(dir => Path.Combine(dir, ClaudeExeRelativePath))
                .FirstOrDefault(_fileSystem.FileExists);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Direct strategy denied access: {ex.Message}");
            return null;
        }
    }

    private string? ResolveViaPowerShell()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-Command \"(Get-AppxPackage | Where-Object { $_.Name -like '*Claude*' }).InstallLocation\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            _logger.LogWarning("PowerShell strategy: Process.Start returned null");
            return null;
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogWarning("PowerShell strategy: empty output from Get-AppxPackage");
            return null;
        }

        var exePath = Path.Combine(output, "app", "claude.exe");
        var exists = _fileSystem.FileExists(exePath);
        if (!exists)
            _logger.LogWarning($"PowerShell strategy: package install location found but claude.exe not at {exePath}");
        return exists ? exePath : null;
    }
}
