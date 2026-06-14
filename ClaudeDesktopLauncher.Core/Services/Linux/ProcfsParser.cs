namespace ClaudeDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Pure-function parsers for the Linux <c>/proc</c> filesystem fields
/// the launcher consumes. Kept separate from
/// <see cref="ProcfsClaudeProcessScanner"/> so the file-walking and
/// the text parsing can be unit-tested independently.
///
/// <para>Field references throughout this file are to <c>man 5 proc</c>:
/// <list type="bullet">
///   <item><c>/proc/stat</c> -- system-wide stats; first <c>btime</c>
///   line gives the boot time as a Unix timestamp in seconds.</item>
///   <item><c>/proc/PID/stat</c> -- space-separated fields where (1)
///   is pid, (2) is comm in parens, (4) is ppid, (22) is starttime
///   in clock ticks since boot. The comm field can contain spaces and
///   parens, so we split on the last <c>)</c> not the first.</item>
///   <item><c>/proc/PID/cmdline</c> -- NUL-separated argv array.</item>
/// </list></para>
/// </summary>
public static class ProcfsParser
{
    /// <summary>Number of clock ticks per second (<c>sysconf(_SC_CLK_TCK)</c>);
    /// 100 on every Linux distribution shipping a stock kernel.</summary>
    public const int ClockTicksPerSecond = 100;

    /// <summary>
    /// Returns the Unix-timestamp boot time from a <c>/proc/stat</c>
    /// blob. The file contains many lines; we want the one starting
    /// with <c>"btime "</c>. Returns 0 if not found (caller will then
    /// produce nonsense StartTime values, which is preferable to
    /// throwing since the scanner must never crash the launcher).
    /// </summary>
    public static long ParseBootTime(string procStatContent)
    {
        foreach (var line in procStatContent.Split('\n'))
        {
            if (!line.StartsWith("btime ")) continue;
            return long.TryParse(line.AsSpan(6).Trim(), out var t) ? t : 0;
        }
        return 0;
    }

    /// <summary>
    /// Parses a <c>/proc/PID/stat</c> line into (ppid, starttime_ticks).
    /// The comm field (2) is parenthesised and can contain spaces, so
    /// we anchor on the LAST <c>)</c> in the line then split the
    /// remainder. Returns null on malformed input.
    /// </summary>
    public static (int Ppid, long StartTicks)? ParseStat(string statContent)
    {
        var lastParen = statContent.LastIndexOf(')');
        if (lastParen < 0 || lastParen + 2 >= statContent.Length) return null;

        // Fields after the comm: state ppid pgrp ... starttime ...
        // The first char after ") " is the state; field index in the
        // remainder where 0=state, 1=ppid, ..., 19=starttime (since
        // the leading pid+comm were two fields).
        var rest = statContent[(lastParen + 2)..].Split(' ');
        if (rest.Length < 20) return null;
        if (!int.TryParse(rest[1], out var ppid)) return null;
        if (!long.TryParse(rest[19], out var starttime)) return null;
        return (ppid, starttime);
    }

    /// <summary>
    /// Converts a NUL-separated <c>/proc/PID/cmdline</c> blob into a
    /// space-separated command line resembling the WMI CommandLine
    /// field on Windows. Trailing NULs (which most cmdline files have)
    /// are trimmed so the result doesn't end in whitespace.
    /// </summary>
    public static string ParseCmdline(byte[] cmdlineBytes)
    {
        if (cmdlineBytes.Length == 0) return string.Empty;
        var s = System.Text.Encoding.UTF8.GetString(cmdlineBytes);
        return s.Replace('\0', ' ').TrimEnd();
    }

    /// <summary>
    /// True if a command line represents a "main" Claude Electron
    /// process: it must reference <c>app.asar</c> AND must NOT carry
    /// an Electron child-process flag (<c>--type=zygote</c>,
    /// <c>--type=renderer</c>, <c>--type=gpu-process</c>, etc.). The
    /// main process is the one the launcher tracks per slot; the
    /// children are aggregated separately into ChildProcessIds for
    /// RAM/CPU totals.
    /// </summary>
    public static bool IsMainClaudeElectron(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return false;
        if (!commandLine.Contains("app.asar")) return false;
        if (commandLine.Contains("--type=")) return false;
        return true;
    }

    /// <summary>
    /// True if the directory name under /proc represents a PID (all
    /// digits). /proc also contains non-PID entries like "sys",
    /// "bus", "cpuinfo", "self", "thread-self" which must be skipped.
    /// </summary>
    public static bool IsPidDirectoryName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var c in name)
            if (c < '0' || c > '9') return false;
        return true;
    }
}
