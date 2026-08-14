using System.Runtime.Versioning;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// One process row as read from WMI: pid, parent pid, and command line.
/// </summary>
/// <remarks>
/// Was a PRIVATE nested record inside <see cref="WmiClaudeProcessScanner"/>. Promoted to namespace
/// scope when the query moved here - a nested private type is invisible to the extracted class.
/// </remarks>
internal record ProcessRecord(int Pid, int ParentPid, string CommandLine);

/// <summary>
/// A live claude.exe: its pid, and the start time that identifies WHICH process that pid currently
/// refers to.
/// </summary>
/// <remarks>
/// StartTicks is not decoration. Windows reuses PIDs, so without it a recycled pid would silently
/// serve the cached command line of a process that no longer exists - a wrong answer, which is worse
/// than the cost this whole class exists to remove.
/// </remarks>
internal readonly record struct LiveClaudeProcess(int Pid, long StartTicks);

/// <summary>
/// Reads command line and parent linkage for the running claude.exe processes, and CACHES them,
/// because a running process's command line never changes.
/// </summary>
/// <remarks>
/// ⚠️ 2026-08-11. The original query was
/// <c>SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process WHERE Name='claude.exe'</c>,
/// polled every 2 seconds by <see cref="SlotProcessMonitor"/>. Measured on a 24-core machine, that
/// cost <b>1.45 CPU-seconds per query</b> inside the WmiPrvSE host serving CIMWin32 - roughly 71% of
/// a core, permanently. Over one 53-hour session that host accumulated <b>49.7 hours</b> of CPU and
/// sat at 113% of a core; stopping this app dropped it to 14.8% within seconds, which is what
/// confirmed the cause.
///
/// ⚠️ 2026-08-14, AND THIS IS WHY IT MATTERS: the app was reopened and the damage came straight
/// back. A WmiPrvSE host 41.9 hours old had accumulated <b>22.0 hours</b> of CPU and was burning
/// ~1.8 cores at the moment of measurement. The fix below sat unmerged for a day and the machine
/// paid for every hour of it.
///
/// The reason is not the filter. WMI applies <c>WHERE</c> AFTER the provider enumerates, and asking
/// for <c>CommandLine</c> forces it to open each process and read its PEB - so a query returning 14
/// rows walks all ~550 processes.
///
/// ⚠️ NARROWING THE PREDICATE BARELY HELPS, and it is worth recording that this was measured rather
/// than assumed. Rewriting it as <c>WHERE ProcessId = a OR ProcessId = b ...</c> gave 1.09 s against
/// 1.45 s - a 25% saving, not the order of magnitude the shape of the query suggests. WMI still
/// enumerates.
///
/// What actually works is not asking. A live process's command line and parent are IMMUTABLE, so
/// they only need reading once. The PID set is obtained via
/// <see cref="System.Diagnostics.Process.GetProcessesByName(string)"/> at ~143 ms and no WMI at all;
/// WMI is consulted only for PIDs never seen before. In steady state - the launcher sitting in the
/// tray with Claude running and nothing starting or stopping - that is <b>zero WMI queries</b>.
///
/// That last sentence was a comment and nothing more until 2026-08-14. It is now the subject of
/// <c>ClaudeProcessWmiQueryTests</c>, which counts the calls. A performance claim no test can see is
/// a claim that quietly stops being true.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class ClaudeProcessWmiQuery
{
    // Keyed by pid; StartTicks guards against PID reuse handing back a stale command line.
    private readonly Dictionary<int, CacheEntry> _cache = [];

    private readonly record struct CacheEntry(long StartTicks, int ParentPid, string CommandLine);

    private readonly Func<IReadOnlyList<LiveClaudeProcess>> _listLive;
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<ProcessRecord>> _readCommandLines;

    /// <summary>Production wiring: real process list, real WMI.</summary>
    public ClaudeProcessWmiQuery()
        : this(ClaudeProcessSources.ListLiveClaudeProcesses, ClaudeProcessSources.ReadCommandLinesFromWmi)
    {
    }

    /// <summary>
    /// Test seam. Both sources are injected rather than faked behind an interface: there are exactly
    /// two, they are pure functions of their inputs, and an interface with two members implemented
    /// once in production and once in tests is ceremony around a delegate.
    /// </summary>
    internal ClaudeProcessWmiQuery(
        Func<IReadOnlyList<LiveClaudeProcess>> listLive,
        Func<IReadOnlyList<int>, IReadOnlyList<ProcessRecord>> readCommandLines)
    {
        _listLive = listLive;
        _readCommandLines = readCommandLines;
    }

    /// <summary>
    /// Command line and parent linkage for every running claude.exe, reading WMI only for processes
    /// not already known.
    /// </summary>
    public List<ProcessRecord> QueryClaudeProcesses()
    {
        var live = _listLive();
        if (live.Count == 0)
        {
            _cache.Clear();
            return [];
        }

        // Drop entries whose process is gone, or whose PID has been reused by a different process.
        var livePids = new Dictionary<int, long>(live.Count);
        foreach (var p in live) { livePids[p.Pid] = p.StartTicks; }
        foreach (var pid in _cache.Keys.ToList())
        {
            if (!livePids.TryGetValue(pid, out var ticks) || ticks != _cache[pid].StartTicks)
            {
                _cache.Remove(pid);
            }
        }

        var unknown = live.Where(p => !_cache.ContainsKey(p.Pid)).ToList();
        if (unknown.Count > 0)
        {
            FillCache(unknown);
        }

        var results = new List<ProcessRecord>(live.Count);
        foreach (var p in live)
        {
            if (_cache.TryGetValue(p.Pid, out var e))
            {
                results.Add(new ProcessRecord(p.Pid, e.ParentPid, e.CommandLine));
            }
        }
        return results;
    }

    /// <summary>
    /// The one expensive call, restricted to PIDs whose command line is not already known.
    /// </summary>
    private void FillCache(List<LiveClaudeProcess> unknown)
    {
        var ticksByPid = new Dictionary<int, long>(unknown.Count);
        foreach (var p in unknown) { ticksByPid[p.Pid] = p.StartTicks; }

        foreach (var rec in _readCommandLines(unknown.Select(p => p.Pid).ToList()))
        {
            // A pid the source returned but we never asked about started after we listed. Skipping it
            // rather than caching it keeps the cache's start-time guard meaningful: we have no start
            // time for it, so we could not detect a later pid reuse. It is picked up next poll.
            if (!ticksByPid.TryGetValue(rec.Pid, out var ticks)) { continue; }
            _cache[rec.Pid] = new CacheEntry(ticks, rec.ParentPid, rec.CommandLine);
        }
    }
}
