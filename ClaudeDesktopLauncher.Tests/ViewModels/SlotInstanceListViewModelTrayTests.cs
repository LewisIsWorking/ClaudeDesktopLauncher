using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests covering the split between visible and tray-resident slot rows
/// in <c>SlotInstanceListViewModel</c>. Verifies that windowed slots
/// land in <c>Items</c>, tray-resident slots land in <c>TrayItems</c>,
/// mixed states route correctly, <c>HasTrayItems</c> reflects the tray
/// collection size, and a slot transitioning between states moves
/// between collections without leaving a stale row.
/// </summary>
public class SlotInstanceListViewModelTrayTests
{
    private readonly SlotInstanceListViewModelTestFixture _f = new();

    [Fact]
    public void Refresh_WindowedSlot_LandsInItems_NotInTrayItems()
    {
        _f.ReturnSlotsWithTray((100, 1, false));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Single(sut.Items);
        Assert.Equal(1, sut.Items[0].InstanceNumber);
        Assert.Empty(sut.TrayItems);
    }

    [Fact]
    public void Refresh_TrayResidentSlot_LandsInTrayItems_NotInItems()
    {
        _f.ReturnSlotsWithTray((100, 1, true));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Empty(sut.Items);
        Assert.Single(sut.TrayItems);
        Assert.Equal(1, sut.TrayItems[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_MixedWindowedAndTray_RoutesCorrectly()
    {
        _f.ReturnSlotsWithTray(
            (100, 1, false),  // visible
            (200, 2, true),   // tray
            (300, 3, false),  // visible
            (400, 4, true));  // tray
        var sut = _f.CreateSut();

        sut.Refresh(new[]
        {
            SlotInstanceListViewModelTestFixture.Snap(100),
            SlotInstanceListViewModelTestFixture.Snap(200),
            SlotInstanceListViewModelTestFixture.Snap(300),
            SlotInstanceListViewModelTestFixture.Snap(400)
        });

        Assert.Equal(2, sut.Items.Count);
        Assert.Equal(new[] { 1, 3 }, sut.Items.Select(i => i.InstanceNumber));
        Assert.Equal(2, sut.TrayItems.Count);
        Assert.Equal(new[] { 2, 4 }, sut.TrayItems.Select(i => i.InstanceNumber));
    }

    [Fact]
    public void HasTrayItems_IsFalse_WhenNoTrayItems()
    {
        _f.ReturnSlotsWithTray((100, 1, false));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.False(sut.HasTrayItems);
    }

    [Fact]
    public void HasTrayItems_IsTrue_WhenTrayItemsPresent()
    {
        _f.ReturnSlotsWithTray((100, 1, true));
        var sut = _f.CreateSut();

        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.True(sut.HasTrayItems);
    }

    [Fact]
    public void Refresh_SlotMovesFromVisibleToTray_LeavesVisibleAndAppearsInTray()
    {
        _f.ReturnSlotsWithTray((100, 1, false));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });
        Assert.Single(sut.Items);
        Assert.Empty(sut.TrayItems);

        // User close-to-trays the slot: same PID, same slot number, now tray-resident
        _f.ReturnSlotsWithTray((100, 1, true));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Empty(sut.Items);
        Assert.Single(sut.TrayItems);
        Assert.Equal(1, sut.TrayItems[0].InstanceNumber);
    }

    [Fact]
    public void Refresh_SlotMovesFromTrayToVisible_LeavesTrayAndAppearsInItems()
    {
        _f.ReturnSlotsWithTray((100, 1, true));
        var sut = _f.CreateSut();
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });
        Assert.Single(sut.TrayItems);

        // User clicks the slot's tray icon to restore the window
        _f.ReturnSlotsWithTray((100, 1, false));
        sut.Refresh(new[] { SlotInstanceListViewModelTestFixture.Snap(100) });

        Assert.Single(sut.Items);
        Assert.Empty(sut.TrayItems);
    }
}
