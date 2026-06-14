using ClaudeDesktopLauncher.Core.Models;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for <c>ExternalInstanceListViewModel.Refresh</c> covering how
/// external rows are discovered via the scanner/classifier, how snapshots
/// are correlated by PID for live CPU/RAM/uptime updates, item-identity
/// preservation across refreshes, removal of PIDs that have closed, the
/// recomputed totals surface, and the warning-on-scanner-failure path.
/// Close-button confirm-dialog behaviour lives in a separate test class.
/// </summary>
public class ExternalInstanceListViewModelRefreshTests
{
    private readonly ExternalInstanceListViewModelTestFixture _f = new();

    [Fact]
    public void Refresh_WhenScannerReturnsEmpty_ItemsStaysEmpty()
    {
        _f.Scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = _f.CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
        Assert.False(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_AddsItemForEachExternalProcess()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        var p2 = ExternalInstanceListViewModelTestFixture.Claude(200);
        _f.Scanner.Scan().Returns(new[] { p1, p2 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.Classifier.TryClassifyAsExternal(p2).Returns(ExternalInstanceListViewModelTestFixture.External(200));
        var sut = _f.CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Equal(2, sut.Items.Count);
        Assert.Contains(sut.Items, i => i.Pid == 100);
        Assert.Contains(sut.Items, i => i.Pid == 200);
        Assert.True(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_SkipsSlotProcesses()
    {
        var slotProc = ExternalInstanceListViewModelTestFixture.Claude(100);
        var extProc = ExternalInstanceListViewModelTestFixture.Claude(200);
        _f.Scanner.Scan().Returns(new[] { slotProc, extProc });
        _f.Classifier.TryClassifyAsExternal(slotProc).Returns((ExternalProcessInfo?)null);
        _f.Classifier.TryClassifyAsExternal(extProc).Returns(ExternalInstanceListViewModelTestFixture.External(200));
        var sut = _f.CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Single(sut.Items);
        Assert.Equal(200, sut.Items[0].Pid);
    }

    [Fact]
    public void Refresh_RemovesItemsWhosePidsAreGone()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        Assert.Single(sut.Items);

        _f.Scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
        Assert.False(sut.HasExternalInstances);
    }

    [Fact]
    public void Refresh_PreservesExistingItemIdentityAcrossRefreshes()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        var sut = _f.CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        var firstInstance = sut.Items[0];

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        var secondInstance = sut.Items[0];

        Assert.Same(firstInstance, secondInstance);
    }

    [Fact]
    public void Refresh_CorrelatesSnapshotsByPid_UpdatingExistingItems()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { ExternalInstanceListViewModelTestFixture.Snap(100, cpu: 12.5, ramMb: 300, uptime: TimeSpan.FromMinutes(5)) });

        Assert.Equal(12.5, sut.Items[0].CpuPercent);
        Assert.Equal(300.0, sut.Items[0].RamMb);
    }

    [Fact]
    public void Refresh_IgnoresSnapshotsWithoutMatchingPid()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { ExternalInstanceListViewModelTestFixture.Snap(999, cpu: 50.0) });

        Assert.Equal(0, sut.Items[0].CpuPercent);
    }

    [Fact]
    public void Refresh_RecomputesTotalsFromItems()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        var p2 = ExternalInstanceListViewModelTestFixture.Claude(200);
        _f.Scanner.Scan().Returns(new[] { p1, p2 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.Classifier.TryClassifyAsExternal(p2).Returns(ExternalInstanceListViewModelTestFixture.External(200));
        var sut = _f.CreateSut();

        sut.Refresh(new[]
        {
            ExternalInstanceListViewModelTestFixture.Snap(100, cpu: 5.0, ramMb: 100),
            ExternalInstanceListViewModelTestFixture.Snap(200, cpu: 7.5, ramMb: 200)
        });

        Assert.Equal(300.0, sut.TotalRamMb);
        Assert.Equal(12.5, sut.TotalCpuPercent);
    }

    [Fact]
    public void Refresh_WhenScannerThrows_PreservesPreviousStateAndLogsWarning()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());
        Assert.Single(sut.Items);

        _f.Scanner.Scan().Returns<IReadOnlyList<ClaudeProcessInfo>>(_ => throw new InvalidOperationException("WMI dead"));
        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Single(sut.Items);
        _f.Logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("WMI dead")), Arg.Any<string>());
    }
}
