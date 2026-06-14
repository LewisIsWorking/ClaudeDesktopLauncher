using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class SlotSeedCacheUpdaterTests
{
    private readonly ISlotProcessMonitor _monitor = Substitute.For<ISlotProcessMonitor>();
    private readonly ISlotSeedCache _cache = Substitute.For<ISlotSeedCache>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    // Zero-delay settle so tests don't wait 5 seconds
    private SlotSeedCacheUpdater CreateSut() =>
        new(_monitor, _cache, _logger, TimeSpan.Zero);

    [Fact]
    public void Start_SubscribesToSlotClosedEvent()
    {
        var sut = CreateSut();
        sut.Start();
        _monitor.Received(1).SlotClosed += Arg.Any<EventHandler<SlotClosedEventArgs>>();
    }

    [Fact]
    public void Start_CalledTwice_OnlySubscribesOnce()
    {
        var sut = CreateSut();
        sut.Start();
        sut.Start();
        _monitor.Received(1).SlotClosed += Arg.Any<EventHandler<SlotClosedEventArgs>>();
    }

    [Fact]
    public void Stop_UnsubscribesFromSlotClosedEvent()
    {
        var sut = CreateSut();
        sut.Start();
        sut.Stop();
        _monitor.Received(1).SlotClosed -= Arg.Any<EventHandler<SlotClosedEventArgs>>();
    }

    [Fact]
    public void Stop_WithoutStart_DoesNothing()
    {
        var sut = CreateSut();
        sut.Stop();
        _monitor.DidNotReceiveWithAnyArgs().SlotClosed -= null;
    }

    [Fact]
    public void Dispose_UnsubscribesIfSubscribed()
    {
        var sut = CreateSut();
        sut.Start();
        sut.Dispose();
        _monitor.Received(1).SlotClosed -= Arg.Any<EventHandler<SlotClosedEventArgs>>();
    }

    [Fact]
    public async Task OnSlotClosed_TriggersSnapshotAfterSettleDelay()
    {
        var sut = CreateSut();
        sut.Start();
        var slot = new LaunchSlot(1);

        _monitor.SlotClosed += Raise.EventWith(new SlotClosedEventArgs(slot));

        // Allow the fire-and-forget Task.Run to complete
        await Task.Delay(100);

        _cache.Received(1).TrySnapshot(slot);
    }

    [Fact]
    public async Task OnSlotClosed_WhenSnapshotThrows_DoesNotPropagate()
    {
        var sut = CreateSut();
        sut.Start();
        _cache.TrySnapshot(Arg.Any<LaunchSlot>()).Returns(_ => throw new IOException("boom"));

        // Even though the snapshot throws, the event handler (which runs on a task)
        // should catch the exception. If it didn't, this would produce an unobserved
        // task exception. We simply verify the test does not throw.
        var ex = Record.Exception(() =>
            _monitor.SlotClosed += Raise.EventWith(new SlotClosedEventArgs(new LaunchSlot(2))));
        Assert.Null(ex);

        await Task.Delay(100);
        // The logger should have logged an error. Use Arg.Any<string>() for the
        // caller name because NSubstitute would otherwise fill it from the test
        // method via [CallerMemberName] - but the real call happens inside
        // SlotSeedCacheUpdater.OnSlotClosed, so the caller names wouldn't match.
        _logger.Received().LogError(
            Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<string>());
    }
}