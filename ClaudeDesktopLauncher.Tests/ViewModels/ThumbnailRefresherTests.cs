using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

public class ThumbnailRefresherTests
{
    private readonly IWindowThumbnailService _service = Substitute.For<IWindowThumbnailService>();

    private static ClaudeInstanceViewModel MakeVm(int slot = 1) =>
        new(slot, $"Instance {slot}", isSeeded: true);

    private static ExternalInstanceViewModel MakeExt(int pid = 99) =>
        new(new ExternalProcessInfo(pid, "--user-data-dir=Ext", DateTime.UtcNow));

    [Fact]
    public void RefreshVisibleThumbnails_CallsCaptureForEachVm()
    {
        var vm1 = MakeVm(1);
        var vm2 = MakeVm(2);
        vm1.UpdateFrom(new InstanceResourceSnapshot(10, 1, 0, 0, TimeSpan.Zero));
        vm2.UpdateFrom(new InstanceResourceSnapshot(20, 2, 0, 0, TimeSpan.Zero));

        ThumbnailRefresher.RefreshVisibleThumbnails(_service, [vm1, vm2], 200, 150);

        _service.Received(1).CapturePngBytes(10, 200, 150);
        _service.Received(1).CapturePngBytes(20, 200, 150);
    }

    [Fact]
    public void RefreshVisibleThumbnails_NullBytesFromService_DoesNotThrow()
    {
        var vm = MakeVm(1);
        vm.UpdateFrom(new InstanceResourceSnapshot(10, 1, 0, 0, TimeSpan.Zero));
        _service.CapturePngBytes(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns((byte[]?)null);

        Assert.Null(Record.Exception(() =>
            ThumbnailRefresher.RefreshVisibleThumbnails(_service, [vm], 200, 150)));
    }

    [Fact]
    public void RefreshVisibleThumbnails_EmptyCollection_DoesNotCallService()
    {
        ThumbnailRefresher.RefreshVisibleThumbnails(_service, [], 200, 150);
        _service.DidNotReceive().CapturePngBytes(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void ClearAllThumbnails_DoesNotThrow()
    {
        var vm1 = MakeVm(1);
        var vm2 = MakeVm(2);
        var ext = MakeExt();

        Assert.Null(Record.Exception(() =>
            ThumbnailRefresher.ClearAllThumbnails([vm1, vm2], [ext])));
    }

    [Fact]
    public void HandleToggleChange_WhenEnabled_SavesSettingTrue()
    {
        var settings = new AppSettings { ThumbnailsEnabled = false };
        var savedCount = 0;

        ThumbnailRefresher.HandleToggleChange(true, settings, () => savedCount++, [MakeVm()], []);

        Assert.True(settings.ThumbnailsEnabled);
        Assert.Equal(1, savedCount);
    }

    [Fact]
    public void HandleToggleChange_WhenDisabled_SavesSettingFalse()
    {
        var settings = new AppSettings { ThumbnailsEnabled = true };
        var savedCount = 0;

        ThumbnailRefresher.HandleToggleChange(false, settings, () => savedCount++, [MakeVm()], []);

        Assert.False(settings.ThumbnailsEnabled);
        Assert.Equal(1, savedCount);
    }

    [Fact]
    public void HandleToggleChange_ThreeCollectionOverload_WhenEnabled_Saves()
    {
        var settings = new AppSettings { ThumbnailsEnabled = false };
        var saved = false;

        ThumbnailRefresher.HandleToggleChange(
            true, settings, () => saved = true, [MakeVm()], [MakeVm(2)], [MakeExt()]);

        Assert.True(settings.ThumbnailsEnabled);
        Assert.True(saved);
    }

    [Fact]
    public void HandleToggleChange_ThreeCollectionOverload_WhenDisabled_ClearsAll()
    {
        var settings = new AppSettings { ThumbnailsEnabled = true };
        var saved = false;

        Assert.Null(Record.Exception(() =>
            ThumbnailRefresher.HandleToggleChange(
                false, settings, () => saved = true, [MakeVm()], [MakeVm(2)], [MakeExt()])));

        Assert.False(settings.ThumbnailsEnabled);
        Assert.True(saved);
    }
}
