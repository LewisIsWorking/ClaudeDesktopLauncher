using ClaudeDesktopLauncher.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class VelopackLogApplyFailureDetectorTests
{
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private VelopackLogApplyFailureDetector CreateSut(string logContent, DateTime now)
    {
        var tmpFile = Path.GetTempFileName();
        File.WriteAllText(tmpFile, logContent);
        return new VelopackLogApplyFailureDetector(
            _logger,
            () => tmpFile,
            () => now);
    }

    [Fact]
    public void ApplyFailedRecently_WhenLogHasRecentError_ReturnsTrue()
    {
        var now = new DateTime(2026, 4, 22, 10, 30, 0);
        // Real Velopack format: [HH:MM:SS] [ERROR] Apply error:
        // Entry is within the 2-minute window.
        var log = "[10:29:00] [INFO] Starting apply\n" +
                  "[10:29:30] [ERROR] Apply error: file locked\n" +
                  "[10:29:31] [INFO] Relaunching old version\n";
        var sut = CreateSut(log, now);

        Assert.True(sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void ApplyFailedRecently_WhenLogHasOldError_ReturnsFalse()
    {
        var now = new DateTime(2026, 4, 22, 10, 30, 0);
        // Entry is 15 minutes old — outside the 2-minute window.
        var log = "[10:15:00] [ERROR] Apply error: file locked\n";
        var sut = CreateSut(log, now);

        Assert.False(sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void ApplyFailedRecently_WhenLogHasNoError_ReturnsFalse()
    {
        var now = new DateTime(2026, 4, 22, 10, 30, 0);
        var log = "[10:29:00] [INFO] Starting apply\n[10:29:01] [INFO] Apply succeeded\n";
        var sut = CreateSut(log, now);

        Assert.False(sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void ApplyFailedRecently_WhenLogFileNotFound_ReturnsFalse()
    {
        var sut = new VelopackLogApplyFailureDetector(
            _logger,
            () => @"C:\does\not\exist\velopack.log",
            () => DateTime.Now);

        Assert.False(sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void ApplyFailedRecently_WhenLogFileEmpty_ReturnsFalse()
    {
        var now = new DateTime(2026, 4, 22, 10, 30, 0);
        var sut = CreateSut(string.Empty, now);

        Assert.False(sut.ApplyFailedRecently(TimeSpan.FromMinutes(2)));
    }
}
