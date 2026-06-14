using System.Text.RegularExpressions;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Reads the tail of Velopack's log file to detect whether an
/// apply failure happened in the recent past. See
/// <see cref="IUpdateApplyFailureDetector"/> for the full
/// motivation and v1.10.4 backlog entry for context.
///
/// <para>
/// The log file lives at
/// <c>%LOCALAPPDATA%\ClaudeDesktopLauncher\velopack.log</c>
/// and entries look like:
/// <c>[update:86148] [16:25:25] [ERROR] Apply error: Error applying package: ...</c>
/// Note the timestamp has NO date - just HH:MM:SS. We infer the
/// date by assuming the log line was written today (or yesterday
/// if the current time is very close to midnight and the log
/// timestamp is in the late evening - edge case handled by
/// trying both dates).
/// </para>
/// </summary>
public class VelopackLogApplyFailureDetector : IUpdateApplyFailureDetector
{
    // Match "[HH:MM:SS] [ERROR] Apply error:" anywhere in a line.
    // Use anchored start-of-line optional prefix so we capture the
    // hour:min:sec. Protected against non-greedy weirdness by
    // explicit digit counts.
    private static readonly Regex ApplyErrorPattern = new(
        @"\[(\d{2}):(\d{2}):(\d{2})\]\s+\[ERROR\]\s+Apply error:",
        RegexOptions.Compiled);

    private const int TailBytesToRead = 16_384;

    private readonly ILoggingService _logger;
    private readonly Func<string> _getLogPath;
    private readonly Func<DateTime> _getNow;

    /// <summary>Default constructor for DI - wires in the real log
    /// path and <see cref="DateTime.Now"/>.</summary>
    public VelopackLogApplyFailureDetector(ILoggingService logger)
        : this(logger, ResolveDefaultLogPath, () => DateTime.Now) { }

    /// <summary>Testing-seam constructor - lets tests substitute a
    /// fixed log path (pointing at a test fixture) and a fixed
    /// "now" so time-window logic is deterministic.</summary>
    public VelopackLogApplyFailureDetector(
        ILoggingService logger,
        Func<string> getLogPath,
        Func<DateTime> getNow)
    {
        _logger = logger;
        _getLogPath = getLogPath;
        _getNow = getNow;
    }

    public bool ApplyFailedRecently(TimeSpan recentWindow)
    {
        try
        {
            var logPath = _getLogPath();
            if (!File.Exists(logPath))
            {
                _logger.LogInfo($"Apply-failure detector: no Velopack log at '{logPath}'");
                return false;
            }

            var tail = ReadLogTail(logPath);
            var now = _getNow();
            var threshold = now - recentWindow;
            var found = FindMostRecentApplyError(tail, now, threshold);
            if (found.HasValue)
            {
                _logger.LogWarning(
                    $"Apply-failure detector: Velopack logged apply error at {found.Value:HH:mm:ss} " +
                    $"(within {recentWindow.TotalMinutes:0.#}min threshold). " +
                    $"User-visible banner will be surfaced.");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                $"Apply-failure detector: log read failed, returning false. {ex.Message}");
            return false;
        }
    }

    private static string ReadLogTail(string logPath)
    {
        using var fs = new FileStream(
            logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var start = Math.Max(0, fs.Length - TailBytesToRead);
        fs.Seek(start, SeekOrigin.Begin);
        using var sr = new StreamReader(fs);
        return sr.ReadToEnd();
    }

    private static DateTime? FindMostRecentApplyError(
        string logContent, DateTime now, DateTime threshold)
    {
        DateTime? best = null;
        foreach (Match m in ApplyErrorPattern.Matches(logContent))
        {
            var h = int.Parse(m.Groups[1].Value);
            var mm = int.Parse(m.Groups[2].Value);
            var s = int.Parse(m.Groups[3].Value);
            // Log timestamps have no date - assume "today" first. If
            // the resulting timestamp is in the future (clock skew or
            // the log entry is actually from yesterday near midnight),
            // try yesterday.
            var today = new DateTime(now.Year, now.Month, now.Day, h, mm, s);
            var candidate = today > now ? today.AddDays(-1) : today;
            if (candidate >= threshold && (best is null || candidate > best))
                best = candidate;
        }
        return best;
    }

    private static string ResolveDefaultLogPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeDesktopLauncher", "velopack.log");
}
