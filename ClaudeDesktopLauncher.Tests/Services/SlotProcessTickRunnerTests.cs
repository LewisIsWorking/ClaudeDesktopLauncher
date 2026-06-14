using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class SlotProcessTickRunnerTests
{
    private readonly IClaudeProcessScanner _scanner = Substitute.For<IClaudeProcessScanner>();
    private readonly IClaudeProcessClassifier _classifier = Substitute.For<IClaudeProcessClassifier>();

    private SlotProcessTickRunner CreateSut() => new(_scanner, _classifier);

    private static ClaudeProcessInfo Claude(int pid) =>
        new(pid, "", DateTime.UtcNow);

    /// <summary>
    /// Configures the scanner to return the given Claude processes and the
    /// classifier to map each one to a <see cref="SlotProcessInfo"/> with the
    /// corresponding slot number. Used by every test below so the scanner +
    /// classifier wiring stays out of the assertions.
    /// </summary>
    private void ReturnSlots(params (int pid, int slotNumber)[] mappings)
    {
        var procs = mappings.Select(m => Claude(m.pid)).ToArray();
        _scanner.Scan().Returns(procs);
        for (var i = 0; i < mappings.Length; i++)
        {
            var m = mappings[i];
            _classifier.TryClassifyAsSlot(procs[i]).Returns(new SlotProcessInfo(m.pid, m.slotNumber));
        }
    }

    private void ReturnNoProcesses()
    {
        _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());
    }

    [Fact]
    public void Tick_WhenNoProcesses_ReturnsEmpty()
    {
        ReturnNoProcesses();
        Assert.Empty(CreateSut().Tick());
    }

    [Fact]
    public void Tick_FirstAppearance_DoesNotEmitClosedEvent()
    {
        ReturnSlots((100, 1));
        Assert.Empty(CreateSut().Tick());
    }

    [Fact]
    public void Tick_WhenKnownSlotDisappears_EmitsClosedEvent()
    {
        var sut = CreateSut();
        ReturnSlots((100, 1));
        sut.Tick();

        ReturnNoProcesses();
        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }

    [Fact]
    public void Tick_WhenOneOfManySlotsDisappears_EmitsOnlyThatOne()
    {
        var sut = CreateSut();
        ReturnSlots((100, 1), (200, 2));
        sut.Tick();

        ReturnSlots((200, 2));
        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }

    [Fact]
    public void Tick_WhenSlotStaysRunning_NoEventEmitted()
    {
        var sut = CreateSut();
        ReturnSlots((100, 1));
        sut.Tick();
        var closed = sut.Tick();
        Assert.Empty(closed);
    }

    [Fact]
    public void Tick_WhenOneSlotClosesAndAnotherStarts_EmitsOnlyClosed()
    {
        var sut = CreateSut();
        ReturnSlots((100, 1));
        sut.Tick();
        ReturnSlots((200, 2));

        var closed = sut.Tick();

        Assert.Single(closed);
        Assert.Equal(1, closed[0].SlotNumber);
    }
}