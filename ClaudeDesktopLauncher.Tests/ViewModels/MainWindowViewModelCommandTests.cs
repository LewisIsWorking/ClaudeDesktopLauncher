using ClaudeDesktopLauncher.Core.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for the RelayCommand-backed commands on <c>MainWindowViewModel</c>:
/// Launch, Refresh resources, Save settings. Focuses on
/// delegating to the right service and updating observable state
/// afterwards (status message, running count, totals). Error-path
/// coverage for LaunchInstances lives here too so the status-message
/// assertion stays co-located with the happy path.
/// </summary>
public class MainWindowViewModelCommandTests
{
    private readonly MainWindowViewModelTestFixture _f = new();

    [Fact]
    public void LaunchInstancesCommand_DelegatesToLauncher()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = _f.CreateSut();
        sut.SlotCount = 2;

        sut.LaunchInstancesCommand.Execute(null);

        _f.Launcher.Received(1).LaunchInstances(2);
    }

    [Fact]
    public void LaunchInstancesCommand_TriggersResourceRefresh()
    {
        // After launch, Refresh() is invoked; RunningInstanceCount is now
        // derived from collection sizes (not the launcher), so the monitor
        // being called is the right invariant to assert here.
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1)]);
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        _f.ResourceMonitor.Received(1).GetSnapshots();
    }

    [Fact]
    public void LaunchInstancesCommand_UpdatesStatusMessageWithLaunchedCount()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Returns([new LaunchSlot(1), new LaunchSlot(2)]);
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("2 instance", sut.StatusMessage);
    }

    [Fact]
    public void LaunchInstancesCommand_WhenExceptionThrown_SetsErrorStatusMessage()
    {
        _f.Launcher.LaunchInstances(Arg.Any<int>()).Throws(new InvalidOperationException("Test error"));
        var sut = _f.CreateSut();

        sut.LaunchInstancesCommand.Execute(null);

        Assert.Contains("Error", sut.StatusMessage);
    }

    [Fact]
    public void RefreshResourcesCommand_UpdatesTotalsFromMonitor()
    {
        _f.ResourceMonitor.GetSnapshots().Returns(new List<InstanceResourceSnapshot>());
        _f.ResourceMonitor.TotalRamMb.Returns(512.0);
        _f.ResourceMonitor.TotalCpuPercent.Returns(8.5);
        var sut = _f.CreateSut();

        sut.Resources.ManualRefreshCommand.Execute(null);

        Assert.Equal(512.0, sut.Resources.TotalRamMb);
        Assert.Equal(8.5, sut.Resources.TotalCpuPercent);
    }

    [Fact]
    public void RefreshResourcesCommand_RefreshesSlotInstances()
    {
        _f.ResourceMonitor.GetSnapshots().Returns(new List<InstanceResourceSnapshot>());
        _f.Scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = _f.CreateSut();

        sut.Resources.ManualRefreshCommand.Execute(null);

        // Slot VM + external VM both use the scanner on refresh, so we
        // expect at least 2 calls (one from each list VM).
        Assert.True(_f.Scanner.ReceivedCalls().Count() >= 2);
    }

    [Fact]
    public void SaveSettingsCommand_PersistsCurrentSlotCount()
    {
        var sut = _f.CreateSut();
        sut.SlotCount = 4;

        sut.SaveSettingsCommand.Execute(null);

        _f.SettingsService.Received().Save(Arg.Is<AppSettings>(s => s.DefaultSlotCount == 4));
    }
}
