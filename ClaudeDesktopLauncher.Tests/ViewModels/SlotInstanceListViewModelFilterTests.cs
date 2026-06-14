using ClaudeDesktopLauncher.Core.Models;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests covering how <c>SlotInstanceListViewModel.Refresh</c> populates
/// the Items collection given classifier output: filtering out non-slot
/// PIDs, relabelling InstanceNumber with the real slot number from the
/// command line rather than the sequential enumeration index, and
/// preserving deterministic sort order.
/// </summary>
public class SlotInstanceListViewModelFilterTests
{
    private readonly SlotInstanceListViewModelTestFixture _f = new();

    [Fact]
    public void Refresh_WhenNoSnapshots_ItemsEmpty()
    {
        _f.Scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
        var sut = _f.CreateSut();

        sut.Refresh(Array.Empty<InstanceResourceSnapshot>());

        Assert.Empty(sut.Items);
    }

    [Fact]
    public void Refresh_AddsRowForEachSlotPid()
    {
        _f.ReturnSlots((100, 1), (200, 2));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100), SlotInstanceListViewModelTestFixture.Snap(200) });

        Assert.Equal(2, sut.Items.Count);
        Assert.Contains(sut.Items, i => i.InstanceNumber == 1);
        Assert.Contains(sut.Items, i => i.InstanceNumber == 2);
    }

    [Fact]
    public void Refresh_SkipsNonSlotPids()
    {
        _f.ReturnSlots((100, 1), (200, null));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100), SlotInstanceListViewModelTestFixture.Snap(200) });

        Assert.Single(sut.Items);
        Assert.Equal(1, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_RelabelsInstanceNumberWithRealSlotNumber()
    {
        // Even if the snapshot came in with InstanceNumber=1 from
        // ResourceMonitor's sequential enumeration, the slot VM should
        // relabel to the real ClaudeSlotN number from the classifier.
        _f.ReturnSlots((100, 3));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100, instanceNumber: 1) });

        Assert.Single(sut.Items);
        Assert.Equal(3, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_SortsBySlotNumber()
    {
        _f.ReturnSlots((100, 3), (200, 1), (300, 2));
        var sut = _f.CreateSut();

        sut.Refresh(new[] {
            SlotInstanceListViewModelTestFixture.Snap(100),
            SlotInstanceListViewModelTestFixture.Snap(200),
            SlotInstanceListViewModelTestFixture.Snap(300)
        });

        Assert.Equal(1, sut.Items[0].InstanceNumber);
        Assert.Equal(2, sut.Items[1].InstanceNumber);
        Assert.Equal(3, sut.Items[2].InstanceNumber);
    }
}
