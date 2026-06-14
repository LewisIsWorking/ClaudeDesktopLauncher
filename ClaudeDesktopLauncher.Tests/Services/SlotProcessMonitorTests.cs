using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Lifecycle tests for the timer-driven <see cref="SlotProcessMonitor"/>.
/// The pure transition logic is covered by
/// <see cref="SlotProcessTickRunnerTests"/>; these tests focus on
/// Start/Stop/Dispose semantics and that exceptions thrown during a tick
/// do not tear down the timer.
/// </summary>
public class SlotProcessMonitorTests
{
    private readonly IClaudeProcessScanner _scanner = Substitute.For<IClaudeProcessScanner>();
    private readonly IClaudeProcessClassifier _classifier = Substitute.For<IClaudeProcessClassifier>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private SlotProcessMonitor CreateSut() => new(_scanner, _classifier, _logger);

    /// <summary>
    /// Configures the scanner to return one Claude process and the
    /// classifier to map it to the given slot number. Tests that model
    /// "slot 1 running" simply call <c>ReturnSlot(1)</c>.
    /// </summary>
    private void ReturnSlot(int slotNumber, int pid = 100)
    {
        var proc = new ClaudeProcessInfo(pid, "", DateTime.UtcNow);
        _scanner.Scan().Returns(new[] { proc });
        _classifier.TryClassifyAsSlot(proc).Returns(new SlotProcessInfo(pid, slotNumber));
    }

    private void ReturnNoSlots() => _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());

    /// <summary>
    /// Polls until <see cref="IClaudeProcessScanner.Scan"/> has been invoked
    /// at least once (a tick has fired), or the 10-second deadline passes.
    /// Cold CI runners can take several hundred milliseconds to deliver the
    /// first timer tick; fixed sleeps are flaky.
    /// </summary>
    private void WaitForTick(int minCalls = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && ScanCallCount() < minCalls)
            Thread.Sleep(50);
    }

    private int ScanCallCount() =>
        _scanner.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IClaudeProcessScanner.Scan));

    [Fact]
    public void Start_BeginsPolling()
    {
        ReturnNoSlots();
        using var sut = CreateSut();

        sut.Start(TimeSpan.FromMilliseconds(50));
        WaitForTick();
        sut.Stop();

        _scanner.Received().Scan();
    }

    [Fact]
    public void Stop_WhenNotStarted_DoesNothing()
    {
        using var sut = CreateSut();
        sut.Stop();
        // No exception thrown means success.
    }

    [Fact]
    public void Dispose_StopsTheTimer()
    {
        var sut = CreateSut();
        ReturnNoSlots();
        sut.Start(TimeSpan.FromMilliseconds(50));
        WaitForTick();

        sut.Dispose();
        _scanner.ClearReceivedCalls();

        Thread.Sleep(200);
        Assert.Equal(0, ScanCallCount());
    }

    [Fact]
    public void Start_CalledTwice_ReplacesPreviousTimer()
    {
        // If the second Start did not replace the first, Scan would not
        // be called within the polling window (the original 10s timer
        // would still be pending). Poll up to 10 seconds for a tick from
        // the 50ms replacement timer.
        using var sut = CreateSut();
        ReturnNoSlots();

        sut.Start(TimeSpan.FromSeconds(10));
        sut.Start(TimeSpan.FromMilliseconds(50));

        WaitForTick();
        sut.Stop();

        _scanner.Received().Scan();
    }

    [Fact]
    public void SlotClosedEvent_RaisedForClosedSlots()
    {
        using var sut = CreateSut();
        LaunchSlot? closedSlot = null;
        var eventRaised = new ManualResetEventSlim(false);

        sut.SlotClosed += (_, args) =>
        {
            closedSlot = args.Slot;
            eventRaised.Set();
        };

        // First tick: slot 1 running. Wait for the runner to observe at
        // least one tick so slot 1 becomes a known slot - otherwise there
        // is nothing to detect as "closed" later.
        ReturnSlot(1);
        sut.Start(TimeSpan.FromMilliseconds(50));
        WaitForTick();

        // Subsequent ticks: slot 1 gone.
        ReturnNoSlots();

        var raised = eventRaised.Wait(TimeSpan.FromSeconds(10));
        sut.Stop();

        Assert.True(raised, "SlotClosed event should have been raised");
        Assert.NotNull(closedSlot);
        Assert.Equal(1, closedSlot!.SlotNumber);
    }

    [Fact]
    public void Tick_WhenScannerThrows_ErrorIsLoggedAndTimerKeepsRunning()
    {
        using var sut = CreateSut();
        var callCount = 0;
        _scanner.Scan().Returns(_ =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("boom");
            return Array.Empty<ClaudeProcessInfo>();
        });

        sut.Start(TimeSpan.FromMilliseconds(50));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && callCount < 2)
            Thread.Sleep(50);
        sut.Stop();

        _logger.Received().LogError(
            Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<string>());
        Assert.True(callCount >= 2,
            $"Timer should have kept polling after the exception, but only polled {callCount} time(s)");
    }
}