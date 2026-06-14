namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Abstracts Velopack's <c>UpdateManager</c> so ViewModels and tests
/// can depend on an interface rather than the concrete Velopack type.
/// The single place that references Velopack's API directly is
/// <see cref="ClaudeDesktopLauncher.Services.VelopackAutoUpdateService"/>.
///
/// <para>
/// v1.10.0 replaces the v1.5.x <c>IUpdateNotifier</c> + <c>IUpdateChecker</c>
/// pair with this single service. The old pair polled GitHub Releases
/// and showed a "new version available" banner; the new service does
/// the check, silent download, and restart-to-apply via Velopack's
/// delta-update infrastructure.
/// </para>
///
/// <para>
/// State-machine contract: callers invoke <see cref="CheckForUpdatesAsync"/>
/// first. On <see cref="UpdateStatus.UpdateAvailable"/>, invoke
/// <see cref="DownloadUpdatesAsync"/> (reports 0-100 progress). On
/// download success, invoke <see cref="ApplyUpdatesAndRestart"/> which
/// exits the current process and hands control to Velopack's updater
/// executable. The new version launches automatically.
/// </para>
/// </summary>
public interface IAutoUpdateService
{
    /// <summary>
    /// Checks the configured GitHub repo for a newer release. Returns
    /// the current status plus the target version string when an
    /// update is available. Network failures surface as
    /// <see cref="UpdateStatus.Failed"/> rather than exceptions so
    /// callers don't need try/catch.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the update payload (delta package preferred, full
    /// package fallback). Must be called only after
    /// <see cref="CheckForUpdatesAsync"/> returned
    /// <see cref="UpdateStatus.UpdateAvailable"/>. Progress callback
    /// receives values 0-100. Returns true on successful download;
    /// false on network or disk error.
    /// </summary>
    Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null);

    /// <summary>
    /// Applies the previously-downloaded update by exiting the
    /// current process, letting Velopack's <c>Update.exe</c> swap
    /// files, and launching the new version. Does not return (the
    /// process exits before this method's continuation would run).
    /// </summary>
    void ApplyUpdatesAndRestart();

    /// <summary>
    /// True exactly once per launch when the current process is the
    /// first-run after a successful update apply. Useful for surfacing
    /// "welcome to v1.10.1 - here's what changed" UI. Read-only; the
    /// value is computed from Velopack's hook state at construction.
    /// </summary>
    bool IsFirstRunAfterUpdate { get; }
}

/// <summary>
/// Result of <see cref="IAutoUpdateService.CheckForUpdatesAsync"/>.
/// <see cref="LatestVersion"/> is non-null only when
/// <see cref="Status"/> is <see cref="UpdateStatus.UpdateAvailable"/>.
/// <see cref="Error"/> is non-null only for
/// <see cref="UpdateStatus.Failed"/>.
/// </summary>
public record UpdateCheckResult(
    UpdateStatus Status,
    string? LatestVersion = null,
    string? Error = null);

/// <summary>
/// Coarse-grained states for the update pipeline. Intentionally
/// flatter than Velopack's internal types so the UI state-machine
/// stays manageable.
/// </summary>
public enum UpdateStatus
{
    NoUpdateAvailable,
    UpdateAvailable,
    Failed
}
