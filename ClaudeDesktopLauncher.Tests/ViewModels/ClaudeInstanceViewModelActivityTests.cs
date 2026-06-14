using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ClaudeInstanceViewModel.LastActiveDisplay"/>,
/// the CPU-activity signal added in v1.10.10.
/// </summary>
public class ClaudeInstanceViewModelActivityTests
{
    private static InstanceResourceSnapshot Snap(int pid, double cpu) =>
        new(pid, 1, cpu, 0, TimeSpan.Zero);

    [Fact]
    public void LastActiveDisplay_BeforeAnyUpdate_IsIdle()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        Assert.Equal("Idle", vm.LastActiveDisplay);
    }

    [Fact]
    public void LastActiveDisplay_AfterHighCpuUpdate_IsActiveNow()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(Snap(1, cpu: 5.0)); // above 3% threshold
        Assert.Equal("Active now", vm.LastActiveDisplay);
    }

    [Fact]
    public void LastActiveDisplay_AfterLowCpuUpdate_RemainsIdle()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(Snap(1, cpu: 1.0)); // below 3% threshold
        Assert.Equal("Idle", vm.LastActiveDisplay);
    }

    [Fact]
    public void LastActiveDisplay_ExactlyAtThreshold_CountsAsActive()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(Snap(1, cpu: 3.0)); // exactly at threshold
        Assert.Equal("Active now", vm.LastActiveDisplay);
    }

    [Fact]
    public void LastActiveDisplay_HighCpuThenLowCpu_RetainsLastActiveTimestamp()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(Snap(1, cpu: 5.0)); // spike sets timestamp
        vm.UpdateFrom(Snap(1, cpu: 0.5)); // idle tick - timestamp preserved
        Assert.Equal("Active now", vm.LastActiveDisplay);
    }

    [Fact]
    public void LastActiveDisplay_JustBelowThreshold_IsIdle()
    {
        var vm = new ClaudeInstanceViewModel(1, "Work", isSeeded: true);
        vm.UpdateFrom(Snap(1, cpu: 2.9)); // just below 3%
        Assert.Equal("Idle", vm.LastActiveDisplay);
    }
}
