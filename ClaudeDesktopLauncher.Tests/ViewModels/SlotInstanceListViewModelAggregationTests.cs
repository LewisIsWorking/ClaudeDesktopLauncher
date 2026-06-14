using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

public class SlotInstanceListViewModelAggregationTests
{
    private static InstanceResourceSnapshot Snap(int pid, double cpu = 0, long ram = 0) =>
        new(pid, 1, cpu, ram, TimeSpan.Zero);

    private static ClaudeProcessInfo ProcWithChildren(int pid, params int[] childPids) =>
        new(pid, $"--user-data-dir=Slot{pid}", DateTime.UtcNow,
            IsWindowed: true, ChildProcessIds: childPids.ToList());

    private static ClaudeProcessInfo ProcNoChildren(int pid) =>
        new(pid, $"--user-data-dir=Slot{pid}", DateTime.UtcNow);

    [Fact]
    public void AggregateChildSnapshots_NoChildPids_ReturnsSameSnapshots()
    {
        var snaps = new List<InstanceResourceSnapshot> { Snap(1, cpu: 5, ram: 100) };
        var procs = new List<ClaudeProcessInfo> { ProcNoChildren(1) };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        Assert.Single(result);
        Assert.Equal(5, result[0].CpuPercent);
        Assert.Equal(100, result[0].RamBytes);
    }

    [Fact]
    public void AggregateChildSnapshots_AddsChildRamToParent()
    {
        var snaps = new List<InstanceResourceSnapshot>
        {
            Snap(1, ram: 100),
            Snap(10, ram: 50),
            Snap(11, ram: 75),
        };
        var procs = new List<ClaudeProcessInfo> { ProcWithChildren(1, 10, 11) };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        var parent = result.First(s => s.ProcessId == 1);
        Assert.Equal(225, parent.RamBytes);
    }

    [Fact]
    public void AggregateChildSnapshots_AddsChildCpuToParent()
    {
        var snaps = new List<InstanceResourceSnapshot>
        {
            Snap(1, cpu: 1.0),
            Snap(10, cpu: 2.0),
            Snap(11, cpu: 0.5),
        };
        var procs = new List<ClaudeProcessInfo> { ProcWithChildren(1, 10, 11) };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        var parent = result.First(s => s.ProcessId == 1);
        Assert.Equal(3.5, parent.CpuPercent);
    }

    [Fact]
    public void AggregateChildSnapshots_MissingChildSnapshot_SkippedGracefully()
    {
        var snaps = new List<InstanceResourceSnapshot> { Snap(1, ram: 100) };
        // child PID 99 has no snapshot (e.g. already exited)
        var procs = new List<ClaudeProcessInfo> { ProcWithChildren(1, 99) };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        var parent = result.First(s => s.ProcessId == 1);
        Assert.Equal(100, parent.RamBytes);
    }

    [Fact]
    public void AggregateChildSnapshots_MultipleSlots_AggregatesIndependently()
    {
        var snaps = new List<InstanceResourceSnapshot>
        {
            Snap(1, ram: 100),
            Snap(2, ram: 200),
            Snap(10, ram: 50),
            Snap(20, ram: 80),
        };
        var procs = new List<ClaudeProcessInfo>
        {
            ProcWithChildren(1, 10),
            ProcWithChildren(2, 20),
        };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        Assert.Equal(150, result.First(s => s.ProcessId == 1).RamBytes);
        Assert.Equal(280, result.First(s => s.ProcessId == 2).RamBytes);
    }

    [Fact]
    public void AggregateChildSnapshots_EmptyChildList_ReturnsSameSnapshot()
    {
        var snaps = new List<InstanceResourceSnapshot> { Snap(1, ram: 100) };
        var procs = new List<ClaudeProcessInfo>
        {
            new(1, "cmd", DateTime.UtcNow, IsWindowed: true, ChildProcessIds: new List<int>())
        };

        var result = SlotInstanceListViewModel.AggregateChildSnapshots(snaps, procs);

        Assert.Equal(100, result.First(s => s.ProcessId == 1).RamBytes);
    }
}
