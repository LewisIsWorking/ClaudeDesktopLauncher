using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

public class SlotCallbackBinderTests
{
    private static SlotInstanceListViewModel MakeSlots() =>
        new(Substitute.For<IClaudeProcessScanner>(),
            Substitute.For<IClaudeProcessClassifier>(),
            Substitute.For<ISlotInitialiser>(),
            Substitute.For<ILoggingService>());

    private static ExternalInstanceListViewModel MakeExternals() =>
        new(Substitute.For<IClaudeProcessScanner>(),
            Substitute.For<IClaudeProcessClassifier>(),
            Substitute.For<IConfirmDialogService>(),
            Substitute.For<IProcessService>(),
            Substitute.For<ILoggingService>());

    [Fact]
    public void Bind_SlotName_ReturnsFromSettings()
    {
        var slots = MakeSlots();
        var settings = new AppSettings();
        settings.SlotNames[2] = "Work";
        SlotCallbackBinder.Bind(slots, settings,
            Substitute.For<IClaudeInstanceLauncher>(),
            Substitute.For<IWindowHider>(),
            Substitute.For<IWindowShower>(),
            Substitute.For<IThumbnailPreviewService>(),
            () => { }, () => { });

        Assert.Equal("Work", slots.GetSlotName!(2));
    }

    [Fact]
    public void Bind_OnSlotNameChanged_PersistsToSettings()
    {
        var slots = MakeSlots();
        var settings = new AppSettings();
        var savedCount = 0;
        SlotCallbackBinder.Bind(slots, settings,
            Substitute.For<IClaudeInstanceLauncher>(),
            Substitute.For<IWindowHider>(),
            Substitute.For<IWindowShower>(),
            Substitute.For<IThumbnailPreviewService>(),
            () => savedCount++, () => { });

        slots.OnSlotNameChanged!(3, "Research");

        Assert.Equal("Research", settings.SlotNames[3]);
        Assert.Equal(1, savedCount);
    }

    [Fact]
    public void Bind_OnKillInstance_KillsAndRefreshes()
    {
        var slots = MakeSlots();
        var launcher = Substitute.For<IClaudeInstanceLauncher>();
        var refreshed = false;
        SlotCallbackBinder.Bind(slots, new AppSettings(), launcher,
            Substitute.For<IWindowHider>(),
            Substitute.For<IWindowShower>(),
            Substitute.For<IThumbnailPreviewService>(),
            () => { }, () => refreshed = true);

        slots.OnKillInstance!(1234);

        launcher.Received(1).KillInstance(1234);
        Assert.True(refreshed);
    }

    [Fact]
    public void Bind_OnHideInstance_ForwardsToHider()
    {
        var slots = MakeSlots();
        var hider = Substitute.For<IWindowHider>();
        SlotCallbackBinder.Bind(slots, new AppSettings(),
            Substitute.For<IClaudeInstanceLauncher>(), hider,
            Substitute.For<IWindowShower>(),
            Substitute.For<IThumbnailPreviewService>(),
            () => { }, () => { });

        slots.OnHideInstance!(9999);

        hider.Received(1).TryHide(9999);
    }

    [Fact]
    public void Bind_OnShowInstance_ForwardsToShower()
    {
        var slots = MakeSlots();
        var shower = Substitute.For<IWindowShower>();
        SlotCallbackBinder.Bind(slots, new AppSettings(),
            Substitute.For<IClaudeInstanceLauncher>(),
            Substitute.For<IWindowHider>(), shower,
            Substitute.For<IThumbnailPreviewService>(),
            () => { }, () => { });

        slots.OnShowInstance!(9999);

        shower.Received(1).TryShow(9999);
    }

    [Fact]
    public void Bind_OnShowPreview_ForwardsToPreviewService()
    {
        var slots = MakeSlots();
        var preview = Substitute.For<IThumbnailPreviewService>();
        SlotCallbackBinder.Bind(slots, new AppSettings(),
            Substitute.For<IClaudeInstanceLauncher>(),
            Substitute.For<IWindowHider>(),
            Substitute.For<IWindowShower>(),
            preview, () => { }, () => { });

        var vm = new ClaudeInstanceViewModel(2, "Work", isSeeded: true);
        slots.OnShowPreview!(vm);

        preview.Received(1).Show(vm.ProcessId, vm.Thumbnail, Arg.Any<string>());
    }

    [Fact]
    public void BindExternal_OnShowPreview_ForwardsToPreviewService()
    {
        var externals = MakeExternals();
        var preview = Substitute.For<IThumbnailPreviewService>();
        SlotCallbackBinder.BindExternal(externals, preview);

        var vm = new ExternalInstanceViewModel(
            new ExternalProcessInfo(99, "--user-data-dir=Ext", DateTime.UtcNow));
        externals.OnShowPreview!(vm);

        preview.Received(1).Show(vm.Pid, vm.Thumbnail, Arg.Any<string>());
    }
}
