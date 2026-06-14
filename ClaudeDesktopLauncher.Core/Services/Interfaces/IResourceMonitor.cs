using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Polls Claude process resource usage and returns per-instance snapshots.
/// CPU % is computed as a delta between consecutive calls.
/// </summary>
public interface IResourceMonitor
{
    IReadOnlyList<InstanceResourceSnapshot> GetSnapshots();
    double TotalRamMb { get; }
    double TotalCpuPercent { get; }
}
