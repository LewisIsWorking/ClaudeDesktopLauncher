using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
using Velopack;
using Velopack.Sources;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Velopack-backed implementation of <see cref="IAutoUpdateService"/>.
/// Talks to Velopack's <see cref="UpdateManager"/> configured with a
/// <see cref="GithubSource"/> pointing at the CoODL releases repo.
///
/// <para>
/// v1.10.0 introduced this service as part of the Velopack migration.
/// The old <c>UpdateNotifier</c> banner + <c>GitHubUpdateChecker</c>
/// pair will be retired in Phase 2; for now both live side-by-side
/// so the wiring can be staged without a flag day.
/// </para>
///
/// <para>
/// Velopack only manages installs produced by <c>vpk pack</c> and
/// installed via the generated <c>Setup.exe</c>. When the app is run
/// directly from a dev build (<c>dotnet run</c>) or from an unmanaged
/// location, <see cref="UpdateManager.IsInstalled"/> is false and the
/// Velopack APIs throw <see cref="NotInstalledException"/>. This
/// service returns <see cref="UpdateStatus.NoUpdateAvailable"/> in
/// that case so the UI never shows an update banner during dev work.
/// </para>
/// </summary>
public class VelopackAutoUpdateService : IAutoUpdateService
{
    /// <summary>
    /// GitHub repo URL where Velopack looks for published releases.
    /// Hardcoded because it's a compile-time constant of the product;
    /// if we ever ship a fork under a different org, change it here
    /// and rebuild.
    /// </summary>
    private const string GithubRepoUrl =
        "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";

    /// <summary>
    /// Set by the <c>VelopackApp.Build().OnFirstRun(...)</c> hook at
    /// the very start of <c>App.OnFrameworkInitializationCompleted</c>.
    /// Reading this field in the service lets VMs surface "you just
    /// updated to vX.Y.Z" UI without needing direct access to the
    /// Velopack hook infrastructure.
    /// </summary>
    public static bool FirstRunAfterUpdate { get; set; }

    private readonly ILoggingService _logger;
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _pendingUpdate;

    public VelopackAutoUpdateService(ILoggingService logger)
    {
        _logger = logger;
        var source = new GithubSource(GithubRepoUrl, accessToken: null, prerelease: false);
        _updateManager = new UpdateManager(source);
        _logger.LogInfo(
            $"VelopackAutoUpdateService ready. IsInstalled={_updateManager.IsInstalled}, " +
            $"CurrentVersion={_updateManager.CurrentVersion}");
    }

    public bool IsFirstRunAfterUpdate => FirstRunAfterUpdate;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (!_updateManager.IsInstalled)
        {
            _logger.LogInfo("Update check skipped: not a Velopack-managed install (dev build?)");
            return new UpdateCheckResult(UpdateStatus.NoUpdateAvailable);
        }

        try
        {
            var info = await _updateManager.CheckForUpdatesAsync();
            if (info is null)
            {
                _logger.LogInfo("Update check: no newer version available");
                _pendingUpdate = null;
                return new UpdateCheckResult(UpdateStatus.NoUpdateAvailable);
            }

            _pendingUpdate = info;
            var version = info.TargetFullRelease.Version.ToString();
            _logger.LogInfo($"Update check: new version {version} available");
            return new UpdateCheckResult(UpdateStatus.UpdateAvailable, version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Update check failed: {ex.Message}");
            _pendingUpdate = null;
            return new UpdateCheckResult(UpdateStatus.Failed, Error: ex.Message);
        }
    }

    public async Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null)
    {
        if (_pendingUpdate is null)
        {
            _logger.LogWarning("DownloadUpdatesAsync called with no pending update");
            return false;
        }

        try
        {
            _logger.LogInfo($"Downloading update to {_pendingUpdate.TargetFullRelease.Version}...");
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate, p => progress?.Report(p));
            _logger.LogInfo("Update download complete, ready to apply on restart");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Update download failed: {ex.Message}");
            return false;
        }
    }

    public void ApplyUpdatesAndRestart()
    {
        if (_pendingUpdate is null)
        {
            _logger.LogWarning("ApplyUpdatesAndRestart called with no pending update");
            return;
        }

        _logger.LogInfo(
            $"Applying update: current {_updateManager.CurrentVersion} -> target " +
            $"{_pendingUpdate.TargetFullRelease.Version}. Process will exit.");
        _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
