using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Owns the list of launcher-managed Claude slots.
/// Splits running slots across <see cref="Items"/> (windowed) and
/// <see cref="TrayItems"/> (tray-resident) and reconciles both
/// in-place on every poll tick.
///
/// <para>v1.10.9: per-slot RAM and CPU now aggregate the full Electron
/// process tree (renderer, GPU, crashpad, etc.) so slot cards match
/// Windows Task Manager. Child PIDs come from
/// <see cref="ClaudeProcessInfo.ChildProcessIds"/> populated by the
/// WMI scanner; <see cref="AggregateChildSnapshots"/> sums them into
/// the main-process snapshot before <see cref="FilterAndRelabel"/>
/// filters to slot PIDs only.</para>
/// </summary>
public partial class SlotInstanceListViewModel : ObservableObject
{
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;
    private readonly ISlotInitialiser _slotInitialiser;
    private readonly ILoggingService _logger;

    public ObservableCollection<ClaudeInstanceViewModel> Items { get; } = new();
    public ObservableCollection<ClaudeInstanceViewModel> TrayItems { get; } = new();
    public bool HasTrayItems => TrayItems.Count > 0;

    public Func<int, string>? GetSlotName { get; set; }
    public Action<int, string>? OnSlotNameChanged { get; set; }
    public Action<int>? OnKillInstance { get; set; }
    public Action<int>? OnHideInstance { get; set; }
    public Action<int>? OnShowInstance { get; set; }
    public Action<ClaudeInstanceViewModel>? OnShowPreview { get; set; }

    public SlotInstanceListViewModel(
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier,
        ISlotInitialiser slotInitialiser,
        ILoggingService logger)
    {
        _scanner = scanner;
        _classifier = classifier;
        _slotInitialiser = slotInitialiser;
        _logger = logger;
        TrayItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTrayItems));
    }

    public void Refresh(IReadOnlyList<InstanceResourceSnapshot> resourceSnapshots)
    {
        try
        {
            var scanResults = _scanner.Scan();
            var slotByPid = BuildSlotMap(scanResults);
            var snapshotPids = resourceSnapshots.Select(s => s.ProcessId).ToHashSet();

            // Synthesise stubs for tray-resident slots (windowed-only monitor
            // has no snapshot for them). Real uptime, zero CPU/RAM.
            var stubs = slotByPid
                .Where(kvp => kvp.Value.IsTrayResident && !snapshotPids.Contains(kvp.Key))
                .Select(kvp => new InstanceResourceSnapshot(
                    kvp.Key, kvp.Value.SlotNumber, 0, 0,
                    DateTime.UtcNow - kvp.Value.StartTime))
                .ToList();
            var allSnapshots = stubs.Count > 0
                ? resourceSnapshots.Concat(stubs).ToList()
                : resourceSnapshots;

            // Aggregate child-process stats into each main slot snapshot so
            // per-slot cards show full-tree totals (matching Task Manager).
            var aggregated = AggregateChildSnapshots(allSnapshots, scanResults);
            var filtered = FilterAndRelabel(aggregated, slotByPid);
            ReconcileCollection(Items, filtered.Where(s => !s.IsTrayResident).ToList());
            ReconcileCollection(TrayItems, filtered.Where(s => s.IsTrayResident).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Slot instance refresh failed: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------

    private IReadOnlyDictionary<int, (int SlotNumber, bool IsTrayResident, DateTime StartTime)>
        BuildSlotMap(IReadOnlyList<ClaudeProcessInfo> scanResults)
    {
        return scanResults
            .Select(p => (p.ProcessId, Slot: _classifier.TryClassifyAsSlot(p), p.StartTime))
            .Where(x => x.Slot is not null)
            .ToDictionary(
                x => x.ProcessId,
                x => (x.Slot!.SlotNumber, x.Slot!.IsTrayResident, x.StartTime));
    }

    /// <summary>
    /// Sums RAM and CPU from each slot's child processes into the main
    /// process snapshot. Children not present in <paramref name="snapshots"/>
    /// (e.g. already exited) are silently skipped.
    /// </summary>
    internal static IReadOnlyList<InstanceResourceSnapshot> AggregateChildSnapshots(
        IReadOnlyList<InstanceResourceSnapshot> snapshots,
        IReadOnlyList<ClaudeProcessInfo> scanResults)
    {
        if (scanResults.All(p => p.ChildProcessIds is null or { Count: 0 }))
            return snapshots;

        var byPid = snapshots.ToDictionary(s => s.ProcessId);
        var result = snapshots.ToList();

        foreach (var proc in scanResults)
        {
            if (proc.ChildProcessIds is not { Count: > 0 }) continue;
            if (!byPid.TryGetValue(proc.ProcessId, out var main)) continue;

            var extraRam = 0L;
            var extraCpu = 0.0;
            foreach (var childPid in proc.ChildProcessIds)
            {
                if (!byPid.TryGetValue(childPid, out var child)) continue;
                extraRam += child.RamBytes;
                extraCpu += child.CpuPercent;
            }

            if (extraRam == 0 && extraCpu == 0.0) continue;
            var idx = result.IndexOf(main);
            result[idx] = main with
            {
                RamBytes = main.RamBytes + extraRam,
                CpuPercent = Math.Round(main.CpuPercent + extraCpu, 1)
            };
        }
        return result;
    }

    private static IReadOnlyList<InstanceResourceSnapshot> FilterAndRelabel(
        IReadOnlyList<InstanceResourceSnapshot> snapshots,
        IReadOnlyDictionary<int, (int SlotNumber, bool IsTrayResident, DateTime StartTime)> slotByPid)
    {
        return snapshots
            .Where(s => slotByPid.ContainsKey(s.ProcessId))
            .Select(s =>
            {
                var (num, tray, _) = slotByPid[s.ProcessId];
                return s with { InstanceNumber = num, IsTrayResident = tray };
            })
            .OrderBy(s => s.InstanceNumber)
            .ToList();
    }

    private void ReconcileCollection(
        ObservableCollection<ClaudeInstanceViewModel> target,
        IReadOnlyList<InstanceResourceSnapshot> filtered)
    {
        var wantedSlots = filtered.Select(s => s.InstanceNumber).ToHashSet();
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wantedSlots.Contains(target[i].InstanceNumber))
                target.RemoveAt(i);
        }
        foreach (var snap in filtered)
        {
            var row = target.FirstOrDefault(vm => vm.InstanceNumber == snap.InstanceNumber);
            if (row is null)
            {
                var num = snap.InstanceNumber;
                var name = GetSlotName?.Invoke(num) ?? $"Instance {num}";
                var isSeeded = _slotInitialiser.IsSeeded(new LaunchSlot(num));
                row = new ClaudeInstanceViewModel(
                    num, name, isSeeded,
                    (n, v) => OnSlotNameChanged?.Invoke(n, v),
                    p => OnKillInstance?.Invoke(p),
                    p => OnHideInstance?.Invoke(p),
                    p => OnShowInstance?.Invoke(p),
                    vm => OnShowPreview?.Invoke(vm));
                target.Add(row);
            }
            row.UpdateFrom(snap);
        }
    }
}
