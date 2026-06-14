using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Linux <see cref="IClaudeProcessScanner"/> backed by the
/// <c>/proc</c> filesystem. The Linux equivalent of
/// <c>WmiClaudeProcessScanner</c>.
///
/// <para><b>Identification:</b> Claude Desktop on Linux runs as an
/// Electron app; the OS-visible process name (<c>/proc/PID/comm</c>)
/// is "electron" for every part of the tree. The "main" process is
/// the one whose <c>cmdline</c> references <c>app.asar</c> and lacks
/// a <c>--type=...</c> child-process flag. Helpers (zygote / renderer
/// / GPU / utility) carry both the cmdline and a <c>--type=</c> flag
/// and are excluded.</para>
///
/// <para><b>Reusing the existing tree analyser:</b> Once mains are
/// identified, the descendant collection mirrors the Windows scanner
/// by building a ppid -> children map across ALL electron PIDs and
/// handing it to <see cref="ClaudeProcessTreeAnalyser"/> -- a pure
/// function that's already tested and platform-agnostic.</para>
///
/// <para><b>IsWindowed:</b> Wayland forbids cross-process window
/// enumeration without portal consent. The Linux MVP always reports
/// <c>true</c>, so every running Claude shows up as a visible slot
/// or external instance; the tray-resident path is unreachable on
/// Linux until a future Wayland portal integration adds real window
/// tracking. (Hide/show is no-op on Linux anyway.)</para>
/// </summary>
public class ProcfsClaudeProcessScanner : IClaudeProcessScanner
{
    private const string ProcRoot = "/proc";
    private const string ElectronComm = "electron";

    private readonly ILoggingService _logger;
    private long _bootTimeUnixSeconds;

    public ProcfsClaudeProcessScanner(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ClaudeProcessInfo> Scan()
    {
        try
        {
            EnsureBootTimeCached();
            var records = CollectElectronProcesses();
            if (records.Count == 0) return Array.Empty<ClaudeProcessInfo>();

            var childPidsByParent = records
                .GroupBy(r => r.Ppid)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.Select(r => r.Pid).ToList());

            var results = new List<ClaudeProcessInfo>();
            foreach (var r in records)
            {
                if (!ProcfsParser.IsMainClaudeElectron(r.CommandLine)) continue;
                var descendants = ClaudeProcessTreeAnalyser.CollectDescendantPids(
                    r.Pid, childPidsByParent);
                results.Add(new ClaudeProcessInfo(
                    r.Pid, r.CommandLine, StartTimeFor(r.StartTicks),
                    IsWindowed: true,
                    ChildProcessIds: descendants));
            }
            if (results.Count > 0)
                _logger.LogDebug(
                    $"Procfs scan: {results.Count} main Claude electron(s), " +
                    $"{records.Count} total electron(s) inspected");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"ProcfsClaudeProcessScanner failed: {ex.Message}");
            return Array.Empty<ClaudeProcessInfo>();
        }
    }

    /// <summary>
    /// Reads /proc/stat once per scanner lifetime to cache the boot
    /// time. btime never changes during a process's lifetime so this
    /// is safe; rereading on every Scan() would add ~1 ms of disk I/O
    /// per refresh tick for no payoff.
    /// </summary>
    private void EnsureBootTimeCached()
    {
        if (_bootTimeUnixSeconds != 0) return;
        try
        {
            _bootTimeUnixSeconds = ProcfsParser.ParseBootTime(
                File.ReadAllText($"{ProcRoot}/stat"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read /proc/stat for btime: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks <c>/proc</c>, returns one <see cref="ElectronRecord"/>
    /// per process whose <c>comm</c> is "electron". Tolerant of races
    /// (processes vanishing mid-scan) and permission errors (kernel
    /// threads, other users' processes) -- both collapse to skipping
    /// the entry rather than failing the whole scan.
    /// </summary>
    private List<ElectronRecord> CollectElectronProcesses()
    {
        var results = new List<ElectronRecord>();
        foreach (var dir in Directory.EnumerateDirectories(ProcRoot))
        {
            var pidName = Path.GetFileName(dir);
            if (!ProcfsParser.IsPidDirectoryName(pidName)) continue;
            if (!int.TryParse(pidName, out var pid)) continue;

            try
            {
                var comm = File.ReadAllText($"{dir}/comm").TrimEnd('\n');
                if (comm != ElectronComm) continue;

                var statText = File.ReadAllText($"{dir}/stat");
                var parsed = ProcfsParser.ParseStat(statText);
                if (parsed is null) continue;

                var cmdlineBytes = File.ReadAllBytes($"{dir}/cmdline");
                var cmdline = ProcfsParser.ParseCmdline(cmdlineBytes);

                results.Add(new ElectronRecord(
                    pid, parsed.Value.Ppid, cmdline, parsed.Value.StartTicks));
            }
            catch (FileNotFoundException) { /* process exited mid-scan */ }
            catch (DirectoryNotFoundException) { /* same */ }
            catch (UnauthorizedAccessException) { /* not our process */ }
            catch (IOException) { /* transient kernel-side races */ }
        }
        return results;
    }

    /// <summary>
    /// Boot time + (clock ticks / ticks-per-second) seconds, returned
    /// as UTC <see cref="DateTime"/>. Falls back to MinValue when
    /// btime couldn't be read, matching what the Windows scanner does
    /// for processes whose start time can't be read.
    /// </summary>
    private DateTime StartTimeFor(long startTicks)
    {
        if (_bootTimeUnixSeconds == 0) return DateTime.MinValue;
        var unixSeconds = _bootTimeUnixSeconds
            + (startTicks / ProcfsParser.ClockTicksPerSecond);
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
    }

    private record ElectronRecord(int Pid, int Ppid, string CommandLine, long StartTicks);
}
