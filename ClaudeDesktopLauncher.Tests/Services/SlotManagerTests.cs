using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class SlotManagerTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly IClaudeProcessScanner _scanner = Substitute.For<IClaudeProcessScanner>();
    private readonly IClaudeProcessClassifier _classifier = Substitute.For<IClaudeProcessClassifier>();

    private SlotManager CreateSut() => new(_processService, _scanner, _classifier);

    private static ClaudeProcessInfo Claude(int pid, string cmdLine = "") =>
        new(pid, cmdLine, DateTime.UtcNow);

    [Fact]
    public void GetSlots_ReturnsCorrectCount()
    {
        Assert.Equal(3, CreateSut().GetSlots(3).Count);
    }

    [Fact]
    public void GetSlots_SlotsAreNumberedSequentially()
    {
        var slots = CreateSut().GetSlots(3);

        Assert.Equal(1, slots[0].SlotNumber);
        Assert.Equal(2, slots[1].SlotNumber);
        Assert.Equal(3, slots[2].SlotNumber);
    }

    [Fact]
    public void GetSlots_WhenCountIsZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetSlots(0));
    }

    [Fact]
    public void GetNextAvailableSlot_UsesWindowedCountPlusOne()
    {
        _processService.CountByNameWithWindow("claude").Returns(2);

        Assert.Equal(3, CreateSut().GetNextAvailableSlot().SlotNumber);
    }

    [Fact]
    public void GetNextAvailableSlot_WhenNoneRunning_ReturnsSlotOne()
    {
        _processService.CountByNameWithWindow("claude").Returns(0);

        Assert.Equal(1, CreateSut().GetNextAvailableSlot().SlotNumber);
    }

    [Fact]
    public void GetNextAvailableSlot_DoesNotUseRawCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(0);

        CreateSut().GetNextAvailableSlot();

        _processService.DidNotReceive().CountByName(Arg.Any<string>());
    }

    [Fact]
    public void GetNextFreeSlots_WhenNoneOccupied_ReturnsSlotsStartingFromOne()
    {
        _scanner.Scan().Returns(Array.Empty<ClaudeProcessInfo>());

        var slots = CreateSut().GetNextFreeSlots(3);

        Assert.Equal(new[] { 1, 2, 3 }, slots.Select(s => s.SlotNumber));
    }

    [Fact]
    public void GetNextFreeSlots_SkipsOccupiedSlots()
    {
        var proc1 = Claude(100);
        var proc3 = Claude(101);
        _scanner.Scan().Returns(new[] { proc1, proc3 });
        _classifier.TryClassifyAsSlot(proc1).Returns(new SlotProcessInfo(100, 1));
        _classifier.TryClassifyAsSlot(proc3).Returns(new SlotProcessInfo(101, 3));

        var slots = CreateSut().GetNextFreeSlots(3);

        Assert.Equal(new[] { 2, 4, 5 }, slots.Select(s => s.SlotNumber));
    }

    [Fact]
    public void GetNextFreeSlots_WhenCountIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetNextFreeSlots(0));
    }

    [Fact]
    public void GetNextFreeSlots_WhenCountIsNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSut().GetNextFreeSlots(-1));
    }

    [Fact]
    public void GetNextFreeSlots_WhenAllSlotsInRangeOccupied_Throws()
    {
        var procs = Enumerable.Range(1, 100)
            .Select(n => Claude(1000 + n))
            .ToArray();
        _scanner.Scan().Returns(procs);
        for (var n = 1; n <= 100; n++)
        {
            var slotNumber = n;
            _classifier.TryClassifyAsSlot(procs[n - 1])
                .Returns(new SlotProcessInfo(1000 + slotNumber, slotNumber));
        }

        Assert.Throws<InvalidOperationException>(() => CreateSut().GetNextFreeSlots(1));
    }

    [Fact]
    public void GetNextFreeSlots_IgnoresExternalProcesses()
    {
        // External Claude (default profile) is classified as null by
        // TryClassifyAsSlot, so SlotManager should not see it and slot 1
        // should still be free.
        var external = Claude(999);
        _scanner.Scan().Returns(new[] { external });
        _classifier.TryClassifyAsSlot(external).Returns((SlotProcessInfo?)null);

        var slots = CreateSut().GetNextFreeSlots(1);

        Assert.Equal(1, slots[0].SlotNumber);
    }
}