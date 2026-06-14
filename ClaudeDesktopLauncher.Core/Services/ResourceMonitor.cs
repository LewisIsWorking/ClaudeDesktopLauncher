using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Polls Claude process resource usage. CPU % is computed as a delta
/// between the current poll and the previous one — call GetSnapshots()
/// on a regular interval (e.g. every 5s) for meaningful CPU readings.
/// First call always returns 0% CPU (no previous baseline to compare).
///
/// <para>
/// v1.10.7: switched from <c>GetWindowedProcessSnapshots</c> to
/// <c>GetAllProcessSnapshots</c> so that Total RAM and CPU include the
/// full process tree (browser-main + renderer + GPU + crashpad + network
/// service + node-service, etc.) matching what Windows Task Manager
/// reports. Per-slot card numbers still show the browser-main process
/// only (requires tree aggregation — tracked in the backlog); the
/// improvement here is visible in the resource totals row.
/// </para>
/// </summary>
public class ResourceMonitor : IResourceMonitor
{
    private readonly IProcessService _processService;
    private Dictionary<int, ProcessSnapshot> _previousSnapshots = new();

    public double TotalRamMb { get; private set; }
    public double TotalCpuPercent { get; private set; }

    public ResourceMonitor(IProcessService processService)
    {
        _processService = processService;
    }

    public IReadOnlyList<InstanceResourceSnapshot> GetSnapshots()
    {
        // Get ALL claude processes (main + children) so totals match
        // Task Manager. Per-slot reconciliation downstream filters to
        // main PIDs via the scanner; child-process snapshots are silently
        // dropped there, but contribute correctly to the totals here.
        var current = _processService.GetAllProcessSnapshots("claude");
        var results = current
            .Select((snapshot, index) => BuildSnapshot(snapshot, index + 1))
            .ToList();

        _previousSnapshots = current.ToDictionary(s => s.ProcessId);

        TotalRamMb = Math.Round(results.Sum(s => s.RamMb), 1);
        TotalCpuPercent = Math.Round(results.Sum(s => s.CpuPercent), 1);

        return results;
    }

    private InstanceResourceSnapshot BuildSnapshot(ProcessSnapshot current, int instanceNumber)
    {
        var uptime = current.CapturedAt - current.StartTime;
        var cpuPercent = ComputeCpuPercent(current);

        return new InstanceResourceSnapshot(
            current.ProcessId,
            instanceNumber,
            cpuPercent,
            current.WorkingSetBytes,
            uptime);
    }

    private double ComputeCpuPercent(ProcessSnapshot current)
    {
        if (!_previousSnapshots.TryGetValue(current.ProcessId, out var previous))
            return 0.0;

        var cpuDelta = (current.TotalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
        var timeDelta = (current.CapturedAt - previous.CapturedAt).TotalMilliseconds;

        if (timeDelta <= 0) return 0.0;

        var percent = cpuDelta / (timeDelta * Environment.ProcessorCount) * 100.0;
        return Math.Round(Math.Max(0, Math.Min(100, percent)), 1);
    }
}
