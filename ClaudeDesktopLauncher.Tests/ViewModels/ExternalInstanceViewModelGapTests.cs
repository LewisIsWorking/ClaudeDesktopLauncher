using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

public class ExternalInstanceViewModelGapTests
{
    private static ExternalProcessInfo Info(int pid = 99, string cmdLine = "claude.exe --flag") =>
        new(pid, cmdLine, DateTime.UtcNow);

    [Fact]
    public void ProcessId_AliasesPid()
    {
        var vm = new ExternalInstanceViewModel(Info(pid: 42));
        Assert.Equal(42, vm.ProcessId);
        Assert.Equal(vm.Pid, vm.ProcessId);
    }

    [Fact]
    public void ShowPreviewCommand_InvokesCallback()
    {
        ExternalInstanceViewModel? captured = null;
        var vm = new ExternalInstanceViewModel(Info(), onShowPreview: v => captured = v);
        vm.ShowPreviewCommand.Execute(null);
        Assert.Same(vm, captured);
    }

    [Fact]
    public void ShowPreviewCommand_WithNoCallback_DoesNotThrow()
    {
        var vm = new ExternalInstanceViewModel(Info());
        Assert.Null(Record.Exception(() => vm.ShowPreviewCommand.Execute(null)));
    }

    // Note: UpdateThumbnailFromBytes with valid PNG bytes requires a running
    // Avalonia platform (IPlatformRenderInterface) to construct a Bitmap,
    // which is not available in headless unit tests. The null/empty boundary
    // cases below cover all testable paths without the platform dependency.

    [Fact]
    public void UpdateThumbnailFromBytes_NullBytes_IsNoOp()
    {
        var vm = new ExternalInstanceViewModel(Info());
        Assert.Null(Record.Exception(() => vm.UpdateThumbnailFromBytes(null)));
        Assert.Null(vm.Thumbnail);
    }

    [Fact]
    public void UpdateThumbnailFromBytes_EmptyBytes_IsNoOp()
    {
        var vm = new ExternalInstanceViewModel(Info());
        Assert.Null(Record.Exception(() => vm.UpdateThumbnailFromBytes([])));
        Assert.Null(vm.Thumbnail);
    }
}
