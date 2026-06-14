namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Detects whether Velopack silently failed to apply an update
/// immediately before the current process launch.
///
/// <para>
/// Added in v1.10.4 after three consecutive apply failures were
/// observed on Lewis's machine (2026-04-20) when updating from
/// v1.10.2 to v1.10.3. The symptom: user clicks "Restart to install",
/// Velopack extracts the new payload, tries to back up the current
/// install dir, hits "file in use" (probably Windows Defender
/// scanning the fresh files), retries 10 times, gives up, and
/// silently relaunches the OLD exe. The C# code path never sees
/// the failure - no exception, no return from
/// <see cref="IAutoUpdateService.ApplyUpdatesAndRestart"/>, just
/// the process dying and the old version coming back up. The
/// "Restart to install" banner then reappears and the user clicks
/// it again with the same result.
/// </para>
///
/// <para>
/// The ONLY signal available is Velopack's own log file at
/// <c>%LOCALAPPDATA%\ClaudeDesktopLauncher\velopack.log</c>,
/// which contains <c>[ERROR] Apply error:</c> entries on every
/// failed apply. This service reads the tail of that log on
/// startup and returns true if such an entry exists with a
/// timestamp within the last ~2 minutes (heuristic: the apply
/// attempt itself takes ~15 seconds including retries, and the
/// launcher restarts immediately after, so anything older than
/// 2 minutes is stale).
/// </para>
///
/// <para>
/// Full context and alternative fixes considered in the v1.10.4
/// backlog entry in <c>ROADMAP.md</c> and in the diagnostic-honesty
/// section of <c>docs/dev/LEARNINGS.md</c>.
/// </para>
/// </summary>
public interface IUpdateApplyFailureDetector
{
    /// <summary>
    /// Returns true if a Velopack apply failure was logged within
    /// the last <paramref name="recentWindow"/>. Must not throw;
    /// any I/O error collapses to false (fail closed - better to
    /// miss a detection than crash startup).
    /// </summary>
    bool ApplyFailedRecently(TimeSpan recentWindow);
}
