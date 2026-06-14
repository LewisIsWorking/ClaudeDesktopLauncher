using ClaudeDesktopLauncher.Core.Models;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests covering row lifecycle semantics in
/// <c>SlotInstanceListViewModel</c>: preserving view-model identity
/// across refreshes (so Avalonia bindings don't churn), updating live
/// resource fields in place, removing rows whose slots have closed
/// (including the middle-slot regression), and honouring the slot-name
/// callback + seed-state feed from the initialiser for newly-added rows.
/// </summary>
public class SlotInstanceListViewModelLifecycleTests
{
    private readonly SlotInstanceListViewModelTestFixture _f = new();

    [Fact]
    public void Refresh_PreservesRowIdentityAcrossRefreshes()
    {
        _f.ReturnSlots((100, 1));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });
        var first = sut.Items[0];

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });
        var second = sut.Items[0];

        Assert.Same(first, second);
    }

    [Fact]
    public void Refresh_UpdatesExistingRowResourceFields()
    {
        _f.ReturnSlots((100, 1));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100, cpu: 5.0, ramMb: 100) });

        _f.ReturnSlots((100, 1));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100, cpu: 15.0, ramMb: 250) });

        Assert.Single(sut.Items);
        Assert.Equal(15.0, sut.Items[0].CpuPercent);
        Assert.Equal(250.0, sut.Items[0].RamMb);
    }

    [Fact]
    public void Refresh_RemovesRowsWhoseSlotsAreGone()
    {
        _f.ReturnSlots((100, 1), (200, 2));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100), SlotInstanceListViewModelTestFixture.Snap(200) });

        _f.ReturnSlots((200, 2));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(200) });

        Assert.Single(sut.Items);
        Assert.Equal(2, sut.Items[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_RemovesMiddleSlotCorrectly()
    {
        // Regression: the previous enumeration-index approach would have
        // kept rows 1 and 2 then relabelled snap-for-slot-3 as row 2,
        // visually mis-attributing it.
        _f.ReturnSlots((100, 1), (200, 2), (300, 3));
        var sut = _f.CreateSut();
        sut.Refresh(new[] {
            SlotInstanceListViewModelTestFixture.Snap(100),
            SlotInstanceListViewModelTestFixture.Snap(200),
            SlotInstanceListViewModelTestFixture.Snap(300)
        });

        _f.ReturnSlots((100, 1), (300, 3));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100), SlotInstanceListViewModelTestFixture.Snap(300) });

        Assert.Equal(2, sut.Items.Count);
        Assert.Equal(1, sut.Items[0].InstanceNumber);
        Assert.Equal(3, sut.Items[1].InstanceNumber);
    }

    [Fact]
    public void Refresh_UsesSlotNameCallbackForNewRows()
    {
        _f.ReturnSlots((100, 1));
        var sut = _f.CreateSut();
        sut.GetSlotName = num => num == 1 ? "Primary" : "??";

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Equal("Primary", sut.Items[0].SlotName);
    }

    [Fact]
    public void Refresh_FallsBackToInstanceNameWhenNoCallback()
    {
        _f.ReturnSlots((100, 5));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Equal("Instance 5", sut.Items[0].SlotName);
    }

    [Fact]
    public void Refresh_PassesSeedStateFromInitialiser()
    {
        _f.ReturnSlots((100, 1));
        _f.SlotInitialiser.IsSeeded(new LaunchSlot(1)).Returns(true);
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.True(sut.Items[0].IsSeeded);
    }

    [Fact]
    public void Refresh_WhenScannerThrows_PreservesPreviousStateAndLogsWarning()
    {
        _f.ReturnSlots((100, 1));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });
        Assert.Single(sut.Items);

        _f.Scanner.Scan().Returns<IReadOnlyList<ClaudeProcessInfo>>(
            _ => throw new InvalidOperationException("WMI dead"));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Single(sut.Items);
        _f.Logger.Received(1).LogWarning(
            Arg.Is<string>(s => s.Contains("WMI dead")),
            Arg.Any<string>());
    }
}
