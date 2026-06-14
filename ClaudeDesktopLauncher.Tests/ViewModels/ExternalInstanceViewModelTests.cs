using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

public class ExternalInstanceViewModelTests
{
    private static ExternalProcessInfo Info(
        string cmdLine = @"""C:\Program Files\WindowsApps\Claude\app\claude.exe""",
        int pid = 100,
        DateTime? startTime = null) =>
        new(pid, cmdLine, startTime ?? DateTime.UtcNow);

    [Fact]
    public void Constructor_SetsPidFromInfo()
    {
        var sut = new ExternalInstanceViewModel(Info(pid: 4321));
        Assert.Equal(4321, sut.Pid);
    }

    [Fact]
    public void Constructor_SetsCommandLineFromInfo()
    {
        var sut = new ExternalInstanceViewModel(Info(cmdLine: "claude.exe --flag"));
        Assert.Equal("claude.exe --flag", sut.CommandLine);
    }

    [Fact]
    public void CommandLineDisplay_StripsQuotedExePath()
    {
        var sut = new ExternalInstanceViewModel(Info(
            cmdLine: @"""C:\Program Files\WindowsApps\Claude_1.3109.0.0_x64__x\app\claude.exe"" --arg"));

        Assert.Equal("claude.exe --arg", sut.CommandLineDisplay);
    }

    [Fact]
    public void CommandLineDisplay_StripsUnquotedExePath()
    {
        var sut = new ExternalInstanceViewModel(Info(
            cmdLine: @"C:\Program Files\Claude\claude.exe --arg"));

        Assert.Equal("claude.exe --arg", sut.CommandLineDisplay);
    }

    [Fact]
    public void CommandLineDisplay_WithNoArgs_CollapsesToBareExe()
    {
        var sut = new ExternalInstanceViewModel(Info(
            cmdLine: @"""C:\Program Files\Claude\app\claude.exe"""));

        Assert.Equal("claude.exe", sut.CommandLineDisplay);
    }

    [Fact]
    public void CommandLineDisplay_WithEmptyCommandLine_ShowsFallback()
    {
        var sut = new ExternalInstanceViewModel(Info(cmdLine: ""));

        Assert.Equal("(command line unavailable)", sut.CommandLineDisplay);
    }

    [Fact]
    public void CommandLineDisplay_WithLongCommandLine_AppliesMiddleEllipsis()
    {
        var longArgs = string.Concat(Enumerable.Repeat("--flag ", 30));
        var sut = new ExternalInstanceViewModel(Info(cmdLine: "claude.exe " + longArgs));

        // Should be <= 80 chars with "..." in the middle
        Assert.True(sut.CommandLineDisplay.Length <= 80);
        Assert.Contains("...", sut.CommandLineDisplay);
    }

    [Fact]
    public void Constructor_SetsStartTimeFromInfo()
    {
        var start = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var sut = new ExternalInstanceViewModel(Info(startTime: start));

        Assert.Equal(start, sut.StartTime);
    }

    [Fact]
    public void UpdateFrom_UpdatesCpuRamAndUptime()
    {
        var sut = new ExternalInstanceViewModel(Info());
        var snap = new InstanceResourceSnapshot(
            ProcessId: 100,
            InstanceNumber: 0,
            CpuPercent: 17.5,
            RamBytes: 256L * 1024 * 1024,
            Uptime: TimeSpan.FromMinutes(25));

        sut.UpdateFrom(snap);

        Assert.Equal(17.5, sut.CpuPercent);
        Assert.Equal(256.0, sut.RamMb);
        Assert.Equal(TimeSpan.FromMinutes(25), sut.Uptime);
    }

    [Fact]
    public void UptimeDisplay_UnderOneHour_ShowsMinutesAndSeconds()
    {
        var sut = new ExternalInstanceViewModel(Info());
        sut.UpdateFrom(new InstanceResourceSnapshot(100, 0, 0, 0, TimeSpan.FromSeconds(185)));

        Assert.Equal("3m 5s", sut.UptimeDisplay);
    }

    [Fact]
    public void UptimeDisplay_OverOneHour_ShowsHoursAndMinutes()
    {
        var sut = new ExternalInstanceViewModel(Info());
        sut.UpdateFrom(new InstanceResourceSnapshot(100, 0, 0, 0, TimeSpan.FromMinutes(135)));

        Assert.Equal("2h 15m", sut.UptimeDisplay);
    }

    [Fact]
    public async Task CloseCommand_InvokesCallback()
    {
        ExternalInstanceViewModel? callbackArg = null;
        var sut = new ExternalInstanceViewModel(Info(), vm =>
        {
            callbackArg = vm;
            return Task.CompletedTask;
        });

        await sut.CloseCommand.ExecuteAsync(null);

        Assert.Same(sut, callbackArg);
    }

    [Fact]
    public async Task CloseCommand_WithNullCallback_CompletesWithoutThrowing()
    {
        var sut = new ExternalInstanceViewModel(Info(), onClose: null);

        await sut.CloseCommand.ExecuteAsync(null);

        // No exception = success
        Assert.False(sut.IsClosing);
    }

    [Fact]
    public async Task CloseCommand_SetsIsClosingDuringExecution()
    {
        var tcs = new TaskCompletionSource();
        var sut = new ExternalInstanceViewModel(Info(), _ => tcs.Task);

        var closeTask = sut.CloseCommand.ExecuteAsync(null);
        Assert.True(sut.IsClosing);

        tcs.SetResult();
        await closeTask;
        Assert.False(sut.IsClosing);
    }

    [Fact]
    public async Task CloseCommand_WhenIsClosing_CannotBeReinvoked()
    {
        var tcs = new TaskCompletionSource();
        var sut = new ExternalInstanceViewModel(Info(), _ => tcs.Task);

        var firstCall = sut.CloseCommand.ExecuteAsync(null);

        Assert.False(sut.CloseCommand.CanExecute(null));

        tcs.SetResult();
        await firstCall;
    }
}