namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Raw process data captured at a point in time.
/// Used by ResourceMonitor to compute CPU deltas between polls.
/// </summary>
public record ProcessSnapshot(
    int ProcessId,
    long WorkingSetBytes,
    TimeSpan TotalProcessorTime,
    DateTime StartTime,
    DateTime CapturedAt);
