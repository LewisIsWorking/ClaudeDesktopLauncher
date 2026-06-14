namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Pure function: classifies a collection of claude.exe processes as
/// either "main" (the one the user thinks of as "a Claude window") or
/// "Electron child" (renderer / GPU / utility / crashpad / etc.) based
/// on parent-process identity.
///
/// <para>
/// Rule: a claude.exe is a <b>main</b> process iff its parent is NOT
/// also a claude.exe. Electron's browser process is the root of its
/// own process tree; every child (renderer, GPU, utility) has the
/// main process as its parent. So if you walk the set of running
/// claude.exe PIDs and check each one's <c>ParentProcessId</c>, the
/// ones whose parents aren't in the same set are the mains.
/// </para>
///
/// <para>
/// This identification is orthogonal to window visibility: a
/// close-to-tray'd slot still has a main process (just with
/// <c>MainWindowHandle == 0</c>). The scanner uses this to surface
/// tray-resident slots instead of dropping them with the old
/// windowed-only filter.
/// </para>
///
/// <para>
/// Extracted from <see cref="WmiClaudeProcessScanner"/> so the scanner
/// stays under the 200-line limit and so the classification rule can
/// be unit-tested without needing a real WMI provider.
/// </para>
/// </summary>
public static class ClaudeProcessMainIdentifier
{
    /// <summary>
    /// Returns the subset of PIDs in <paramref name="processes"/> that
    /// are "main" processes (their parent is not also in the set).
    /// Callers should then filter to just these PIDs when building
    /// the UI-visible process list.
    /// </summary>
    public static HashSet<int> IdentifyMainPids(
        IReadOnlyCollection<(int Pid, int ParentPid)> processes)
    {
        var allPids = processes.Select(p => p.Pid).ToHashSet();
        return processes
            .Where(p => !allPids.Contains(p.ParentPid))
            .Select(p => p.Pid)
            .ToHashSet();
    }
}
