using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// The two real data sources behind <see cref="ClaudeProcessWmiQuery"/>: the live process list, and
/// the WMI read that resolves command lines.
/// </summary>
/// <remarks>
/// 2026-08-14: separated from the caching logic so that logic can be tested at all. Everything here
/// talks to the OS and cannot be exercised deterministically; everything in
/// <see cref="ClaudeProcessWmiQuery"/> is now pure with respect to these two functions and is fully
/// covered. The split is along the seam that was already there - one side is policy, the other is
/// I/O - rather than an arbitrary line drawn to hit a coverage number.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class ClaudeProcessSources
{
    /// <summary>
    /// Live claude.exe PIDs with start times, without touching WMI.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT filtered to windowed processes: the caller needs the whole tree, Electron
    /// children included, to resolve parent linkage and identify the "main" process.
    ///
    /// A process can exit between listing and reading StartTime, so that read is guarded; such a
    /// process is simply skipped rather than failing the scan.
    /// </remarks>
    public static IReadOnlyList<LiveClaudeProcess> ListLiveClaudeProcesses()
    {
        var procs = Process.GetProcessesByName("claude");
        try
        {
            var list = new List<LiveClaudeProcess>(procs.Length);
            foreach (var p in procs)
            {
                try
                {
                    list.Add(new LiveClaudeProcess(p.Id, p.StartTime.Ticks));
                }
                catch
                {
                    // Exited, or start time unreadable - skip rather than abort the whole scan.
                }
            }
            return list;
        }
        finally
        {
            foreach (var p in procs) { p.Dispose(); }
        }
    }

    /// <summary>
    /// Reads ParentProcessId and CommandLine for the given PIDs. THE expensive call - see the
    /// measurements on <see cref="ClaudeProcessWmiQuery"/> for why it must be asked as rarely as
    /// possible rather than merely asked more narrowly.
    /// </summary>
    public static IReadOnlyList<ProcessRecord> ReadCommandLinesFromWmi(IReadOnlyList<int> pids)
    {
        if (pids.Count == 0) { return []; }

        var predicate = string.Join(" OR ", pids.Select(pid => $"ProcessId = {pid}"));
        using var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process WHERE {predicate}");

        var results = new List<ProcessRecord>(pids.Count);
        foreach (var obj in searcher.Get())
        {
            using var mo = obj;
            var pid = Convert.ToInt32(mo["ProcessId"]);
            var ppid = mo["ParentProcessId"] is null ? 0 : Convert.ToInt32(mo["ParentProcessId"]);
            var cmd = mo["CommandLine"] as string ?? string.Empty;
            results.Add(new ProcessRecord(pid, ppid, cmd));
        }
        return results;
    }
}
