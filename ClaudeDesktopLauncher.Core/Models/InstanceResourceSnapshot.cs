namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Computed resource usage for a single Claude instance.
/// CPU % is calculated as a delta between two polls.
///
/// <para>
/// <see cref="IsTrayResident"/> is propagated from the classifier via
/// <c>SlotInstanceListViewModel</c> so the view layer can split
/// snapshots into the visible vs hidden sections without re-running
/// classification. It is irrelevant for external Claude snapshots
/// (always false there) and is only meaningful for slot rows.
/// </para>
/// </summary>
public record InstanceResourceSnapshot(
    int ProcessId,
    int InstanceNumber,
    double CpuPercent,
    long RamBytes,
    TimeSpan Uptime,
    bool IsTrayResident = false)
{
    public double RamMb => Math.Round(RamBytes / (1024.0 * 1024.0), 1);

    public string UptimeDisplay => Uptime.TotalHours >= 1
        ? $"{(int)Uptime.TotalHours}h {Uptime.Minutes}m"
        : $"{Uptime.Minutes}m {Uptime.Seconds}s";
}
