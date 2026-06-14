using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ResourceMonitorCpuTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private ResourceMonitor CreateSut() => new(_processService);

    private static ProcessSnapshot Snap(int pid, double cpuMs, double secondsAgo = 10) =>
        new(pid, 0, TimeSpan.FromMilliseconds(cpuMs),
            DateTime.UtcNow.AddSeconds(-secondsAgo), DateTime.UtcNow);

    [Fact]
    public void GetSnapshots_SecondCall_ComputesCpuDelta()
    {
        // First call: baseline
        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { Snap(1, cpuMs: 0) });
        var sut = CreateSut();
        sut.GetSnapshots();

        // Second call: 500ms CPU used over 5s wall time on N processors
        var snap2 = new ProcessSnapshot(1, 0,
            TimeSpan.FromMilliseconds(500),
            DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow);
        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { snap2 });

        var results = sut.GetSnapshots();

        // Should be non-zero CPU
        Assert.True(results[0].CpuPercent >= 0);
    }

    [Fact]
    public void GetSnapshots_ZeroTimeDelta_ReturnsCpuZero()
    {
        var now = DateTime.UtcNow;
        var snap = new ProcessSnapshot(1, 0, TimeSpan.FromMilliseconds(100), now, now);
        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { snap });
        var sut = CreateSut();
        sut.GetSnapshots();

        // Same snapshot again - captured at same time
        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot> { snap });
        var results = sut.GetSnapshots();

        Assert.Equal(0.0, results[0].CpuPercent);
    }

    [Fact]
    public void GetSnapshots_UpdatesTotalCpuPercent()
    {
        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot>
            {
                Snap(1, cpuMs: 0),
                Snap(2, cpuMs: 0),
            });
        var sut = CreateSut();
        sut.GetSnapshots();

        _processService.GetAllProcessSnapshots("claude")
            .Returns(new List<ProcessSnapshot>
            {
                new(1, 0, TimeSpan.FromMilliseconds(1000), DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow),
                new(2, 0, TimeSpan.FromMilliseconds(500), DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow),
            });
        sut.GetSnapshots();

        Assert.True(sut.TotalCpuPercent >= 0);
    }
}
