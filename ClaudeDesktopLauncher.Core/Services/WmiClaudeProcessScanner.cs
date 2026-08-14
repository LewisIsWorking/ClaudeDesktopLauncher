using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Windows-native <see cref="IClaudeProcessScanner"/> using WMI
/// (<c>SELECT ProcessId, ParentProcessId, CommandLine FROM Win32_Process
/// WHERE Name='claude.exe'</c>) to read command lines, parent linkage,
/// and process start times.
///
/// <b>Main-process filter:</b> Claude is an Electron app; each running
/// Claude window spawns ~10 child processes (renderer, GPU, crashpad
/// handler, audio/video/network utility services, node-service). To
/// avoid flooding the UI with a row per child process, the scanner
/// keeps only "main" processes - those whose parent is not also a
/// claude.exe. See <see cref="ClaudeProcessMainIdentifier"/>.
///
/// <b>Windowed vs tray-resident:</b> Before v1.8.2 the scanner also
/// filtered to <c>MainWindowHandle != 0</c>, but that dropped slots
/// the user had close-to-tray'd (window hidden, process tree still
/// alive). Now every main is returned, with <c>IsWindowed</c>
/// reflecting window state so the view layer can split them into the
/// visible-slot list vs the hidden/tray list.
///
/// <b>Command-line enrichment:</b> Chromium/Electron's "browser main"
/// process reports an empty args list to WMI - its
/// <c>--user-data-dir</c> flag is only copied into child processes
/// during fork. Classification (slot vs external) needs that flag, so
/// the scanner walks each main's direct children via
/// <c>ParentProcessId</c> and, when the main's own cmdline is missing
/// the flag, extracts <c>--user-data-dir=...</c> from any child and
/// appends it.
///
/// <b>Child PID aggregation (v1.10.9):</b> After identifying mains,
/// <see cref="ClaudeProcessTreeAnalyser.CollectDescendantPids"/> walks
/// the full process subtree so <see cref="SlotInstanceListViewModel"/>
/// can sum RAM and CPU across all Electron child processes, matching
/// the per-app total Windows Task Manager shows.
///
/// WMI is currently the only reliable way to read another process's
/// command line on Windows from managed code.
/// </summary>
[SupportedOSPlatform("windows")]
public class WmiClaudeProcessScanner : IClaudeProcessScanner
{
    private const string UserDataDirFlag = "--user-data-dir=";

    private readonly ILoggingService _logger;

    // Instance state, not static: it caches command lines across polls, and the scanner is
    // registered as a singleton so the cache lives exactly as long as it should.
    private readonly ClaudeProcessWmiQuery _wmi = new();

    public WmiClaudeProcessScanner(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ClaudeProcessInfo> Scan()
    {
        var all = _wmi.QueryClaudeProcesses();
        if (all.Count == 0) return Array.Empty<ClaudeProcessInfo>();

        var mainPids = ClaudeProcessMainIdentifier.IdentifyMainPids(
            all.Select(p => (p.Pid, p.ParentPid)).ToList());
        if (mainPids.Count == 0) return Array.Empty<ClaudeProcessInfo>();

        var windowedPids = GetWindowedClaudePids();

        var childrenByParent = all
            .GroupBy(p => p.ParentPid)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProcessRecord>)g.ToList());
        // Pid-only map for ClaudeProcessTreeAnalyser (pure function, testable).
        var childPidsByParent = childrenByParent.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<int>)kvp.Value.Select(r => r.Pid).ToList());

        var results = new List<ClaudeProcessInfo>();
        foreach (var pid in mainPids)
        {
            var self = all.FirstOrDefault(p => p.Pid == pid);
            if (self is null) continue; // raced with process exit

            var effectiveCmdline = EnrichWithUserDataDir(self, childrenByParent);
            var isWindowed = windowedPids.Contains(pid);
            // Collect ALL descendant PIDs (renderer, GPU, crashpad, etc.) so
            // SlotInstanceListViewModel can aggregate their RAM/CPU into the
            // per-slot card total, matching what Windows Task Manager shows.
            var descendants = ClaudeProcessTreeAnalyser.CollectDescendantPids(
                pid, childPidsByParent);
            results.Add(new ClaudeProcessInfo(
                pid, effectiveCmdline, TryGetStartTime(pid), isWindowed, descendants));
        }

        return results;
    }

    private static HashSet<int> GetWindowedClaudePids()
    {
        var procs = Process.GetProcessesByName("claude");
        try
        {
            return procs
                .Where(p => p.MainWindowHandle != IntPtr.Zero)
                .Select(p => p.Id)
                .ToHashSet();
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }

    private static string EnrichWithUserDataDir(
        ProcessRecord main,
        IReadOnlyDictionary<int, IReadOnlyList<ProcessRecord>> childrenByParent)
    {
        if (main.CommandLine.Contains(UserDataDirFlag, StringComparison.OrdinalIgnoreCase))
            return main.CommandLine;

        if (!childrenByParent.TryGetValue(main.Pid, out var children))
            return main.CommandLine;

        foreach (var child in children)
        {
            var value = ExtractFlagValue(child.CommandLine, UserDataDirFlag);
            if (value is null) continue;
            var own = main.CommandLine.TrimEnd();
            return $"{own} {UserDataDirFlag}{value}";
        }

        return main.CommandLine;
    }

    /// <summary>
    /// Extracts the value of a <c>--flag=value</c> argument from a command
    /// line. Handles quoted values (<c>--flag="path with space"</c>) and
    /// bare values (<c>--flag=C:\nospaces</c>). Returns <c>null</c> when
    /// the flag is absent.
    /// </summary>
    private static string? ExtractFlagValue(string commandLine, string flagPrefix)
    {
        var idx = commandLine.IndexOf(flagPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var after = commandLine[(idx + flagPrefix.Length)..];
        if (after.StartsWith('"'))
        {
            var close = after.IndexOf('"', 1);
            return close > 0 ? after[1..close] : after[1..];
        }
        var space = after.IndexOf(' ');
        return space > 0 ? after[..space] : after;
    }

    private DateTime TryGetStartTime(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.StartTime;
        }
        catch (ArgumentException)
        {
            return DateTime.MinValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read start time for PID {pid}: {ex.Message}");
            return DateTime.MinValue;
        }
    }

}
