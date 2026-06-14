using Avalonia.Threading;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// State machine for the auto-update pipeline. Sits between
/// <see cref="IAutoUpdateService"/> (which talks to Velopack) and
/// <see cref="MainWindowViewModel"/> (which holds observable UI state).
/// Extracted as a helper class so the VM stays under the 200-line
/// limit and the state transitions have dedicated tests without
/// needing the full VM graph.
///
/// <para>
/// States (<see cref="UpdateUiState"/>): Idle -> Checking ->
/// (NoUpdate | Downloading -> (ReadyToInstall | Failed) | Failed).
/// Only one transition runs at a time; concurrent calls to
/// <see cref="RunCheckAsync"/> while one is already in progress are
/// dropped with a logged warning. Applying the update
/// (<see cref="ApplyAndRestart"/>) terminates the process, so no
/// post-apply state is needed.
/// </para>
///
/// <para>
/// Per the v1.10.0 UX spec (decision 4, option b): check and download
/// run silently on the poll tick when
/// <see cref="AppSettings.AutoCheckForUpdates"/> is true. The UI only
/// surfaces state at <see cref="UpdateUiState.ReadyToInstall"/> (green
/// "Restart to install v1.10.1" banner) or
/// <see cref="UpdateUiState.Failed"/> (amber retry banner). Users who
/// toggle the checkbox off mid-flight see any in-progress download
/// carry through to completion (cancellation would require cooperative
/// token support that Velopack's download doesn't expose cleanly);
/// subsequent polls skip.
/// </para>
/// </summary>
public class UpdateOrchestrator
{
    private readonly IAutoUpdateService _updateService;
    private readonly ILoggingService _logger;
    private readonly Action<UpdateUiState> _onStateChanged;
    private readonly Action<int> _onProgressChanged;
    private readonly Action<string?> _onLatestVersionChanged;

    public UpdateOrchestrator(
        IAutoUpdateService updateService,
        ILoggingService logger,
        Action<UpdateUiState> onStateChanged,
        Action<int> onProgressChanged,
        Action<string?> onLatestVersionChanged)
    {
        _updateService = updateService;
        _logger = logger;
        _onStateChanged = onStateChanged;
        _onProgressChanged = onProgressChanged;
        _onLatestVersionChanged = onLatestVersionChanged;
    }

    public UpdateUiState State { get; private set; } = UpdateUiState.Idle;

    /// <summary>
    /// Runs the full check-then-download pipeline if and only if
    /// auto-update is enabled in settings and no run is already in
    /// progress. Called from the <see cref="MainWindowViewModel"/>
    /// refresh-timer tick. Swallows all errors (surfaces them as
    /// <see cref="UpdateUiState.Failed"/>) so the timer's exception
    /// budget is untouched.
    /// </summary>
    public async Task RunCheckAsync(bool autoCheckEnabled)
    {
        if (!autoCheckEnabled)
        {
            _logger.LogInfo("Update check skipped: auto-check disabled in settings");
            return;
        }
        if (State is UpdateUiState.Checking or UpdateUiState.Downloading)
        {
            _logger.LogInfo($"Update check skipped: already in {State}");
            return;
        }
        if (State is UpdateUiState.ReadyToInstall)
        {
            // Update already downloaded and waiting for restart; don't
            // re-check until either the user restarts or something else
            // resets us to Idle.
            return;
        }

        TransitionTo(UpdateUiState.Checking);
        var result = await _updateService.CheckForUpdatesAsync();

        switch (result.Status)
        {
            case UpdateStatus.NoUpdateAvailable:
                TransitionTo(UpdateUiState.Idle);
                return;
            case UpdateStatus.Failed:
                _logger.LogWarning($"Update check failed: {result.Error}");
                TransitionTo(UpdateUiState.Failed);
                return;
            case UpdateStatus.UpdateAvailable:
                _onLatestVersionChanged(result.LatestVersion);
                await DownloadAsync();
                return;
        }
    }

    /// <summary>
    /// Called from the \"Restart to install\" button in the update
    /// banner. Only valid from <see cref="UpdateUiState.ReadyToInstall\"/>;
    /// ignored in any other state (the button is only visible when we're
    /// in that state, but defensive here too).
    /// </summary>
    public void ApplyAndRestart()
    {
        if (State != UpdateUiState.ReadyToInstall)
        {
            _logger.LogWarning($"ApplyAndRestart called in unexpected state {State}");
            return;
        }
        _updateService.ApplyUpdatesAndRestart();
    }

    /// <summary>
    /// Resets a Failed state back to Idle so the next poll tick
    /// retries the check. Wired to the banner's retry button.
    /// </summary>
    public void Retry()
    {
        if (State == UpdateUiState.Failed)
            TransitionTo(UpdateUiState.Idle);
    }

    /// <summary>
    /// Transitions directly to <see cref="UpdateUiState.ApplyFailed"/>.
    /// Called once at startup when
    /// <see cref="IUpdateApplyFailureDetector"/> confirms a recent
    /// apply failure (Velopack silently relaunched the old exe after
    /// an apply failure - see docs/dev/LEARNINGS.md). Distinct from
    /// <see cref="UpdateUiState.Failed"/> (check/download network
    /// failure) because the UI surfaces different recovery options
    /// - ApplyFailed offers a "Download installer" escape hatch.
    /// </summary>
    public void MarkApplyFailed()
    {
        _logger.LogWarning(
            "UpdateOrchestrator: transitioning to ApplyFailed " +
            "(Velopack apply failure detected in log). User will see " +
            "recovery banner with 'Download installer' option.");
        TransitionTo(UpdateUiState.ApplyFailed);
    }

    private async Task DownloadAsync()
    {
        TransitionTo(UpdateUiState.Downloading);
        _onProgressChanged(0);
        var progress = new Progress<int>(p =>
            Dispatcher.UIThread.Post(() => _onProgressChanged(p)));
        var ok = await _updateService.DownloadUpdatesAsync(progress);
        TransitionTo(ok ? UpdateUiState.ReadyToInstall : UpdateUiState.Failed);
    }

    private void TransitionTo(UpdateUiState newState)
    {
        State = newState;
        _onStateChanged(newState);
    }
}

/// <summary>
/// Coarse UI-facing states for the update pipeline. The banner in
/// <c>LaunchControlsPanel</c> switches visible content based on this
/// value.
/// </summary>
public enum UpdateUiState
{
    Idle,
    Checking,
    Downloading,
    ReadyToInstall,
    Failed,
    /// <summary>v1.10.4+: Velopack silently failed to apply a
    /// previously-downloaded update (e.g. AV scan held file locks
    /// during backup-and-swap). Detected by reading the tail of
    /// velopack.log at startup. UI shows a recovery banner with
    /// instructions to reboot or download the installer manually.</summary>
    ApplyFailed
}
