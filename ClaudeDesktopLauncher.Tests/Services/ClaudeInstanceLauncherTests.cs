using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudeInstanceLauncherTests
{
    private readonly IClaudePathResolver _pathResolver = Substitute.For<IClaudePathResolver>();
    private readonly ISlotManager _slotManager = Substitute.For<ISlotManager>();
    private readonly ISlotInitialiser _slotInitialiser = Substitute.For<ISlotInitialiser>();
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private ClaudeInstanceLauncher CreateSut() =>
        new(_pathResolver, _slotManager, _slotInitialiser, _processService, _logger);

    [Fact]
    public void LaunchSlot_WhenClaudeFound_StartsProcess()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");

        CreateSut().LaunchSlot(new LaunchSlot(1));

        _processService.Received(1).StartWithStderrPipe(
            @"C:\claude.exe",
            Arg.Is<string>(a => a.Contains("ClaudeSlot1")),
            Arg.Any<Action<string>>());
    }

    [Fact]
    public void LaunchSlot_WhenClaudeNotFound_ThrowsInvalidOperationException()
    {
        _pathResolver.ResolveClaudeExePath().Returns((string?)null);

        Assert.Throws<InvalidOperationException>(() => CreateSut().LaunchSlot(new LaunchSlot(1)));
    }

    [Fact]
    public void GetRunningInstanceCount_UsesWindowedCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(3);

        Assert.Equal(3, CreateSut().GetRunningInstanceCount());
    }

    [Fact]
    public void GetRunningInstanceCount_DoesNotUseRawCount()
    {
        _processService.CountByNameWithWindow("claude").Returns(3);

        CreateSut().GetRunningInstanceCount();

        _processService.DidNotReceive().CountByName(Arg.Any<string>());
    }

    [Fact]
    public void KillInstance_DelegatesToProcessService()
    {
        CreateSut().KillInstance(1234);

        _processService.Received(1).KillProcess(1234);
    }

    [Fact]
    public void LaunchInstances_PicksFreeSlotsViaSlotManager()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        _slotManager.GetNextFreeSlots(3).Returns([new LaunchSlot(1), new LaunchSlot(2), new LaunchSlot(3)]);

        CreateSut().LaunchInstances(3);

        _slotManager.Received(1).GetNextFreeSlots(3);
    }

    [Fact]
    public void LaunchInstances_InitialisesEachSlotBeforeLaunching()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        _slotManager.GetNextFreeSlots(2).Returns([new LaunchSlot(1), new LaunchSlot(2)]);

        CreateSut().LaunchInstances(2);

        _slotInitialiser.Received(1).EnsureInitialised(new LaunchSlot(1));
        _slotInitialiser.Received(1).EnsureInitialised(new LaunchSlot(2));
    }

    [Fact]
    public void LaunchInstances_StartsProcessForEachSlot()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        _slotManager.GetNextFreeSlots(2).Returns([new LaunchSlot(1), new LaunchSlot(2)]);

        CreateSut().LaunchInstances(2);

        _processService.Received(1).StartWithStderrPipe(
            @"C:\claude.exe",
            Arg.Is<string>(a => a.Contains("ClaudeSlot1")),
            Arg.Any<Action<string>>());
        _processService.Received(1).StartWithStderrPipe(
            @"C:\claude.exe",
            Arg.Is<string>(a => a.Contains("ClaudeSlot2")),
            Arg.Any<Action<string>>());
    }

    [Fact]
    public void LaunchInstances_ReturnsLaunchedSlots()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        var expected = new[] { new LaunchSlot(1), new LaunchSlot(2) };
        _slotManager.GetNextFreeSlots(2).Returns(expected);

        var result = CreateSut().LaunchInstances(2);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void LaunchInstances_InitialisesBeforeLaunchingInOrder()
    {
        _pathResolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        var slot = new LaunchSlot(1);
        _slotManager.GetNextFreeSlots(Arg.Any<int>()).Returns([slot]);
        var callOrder = new List<string>();
        _slotInitialiser.When(s => s.EnsureInitialised(slot)).Do(_ => callOrder.Add("init"));
        _processService.When(p => p.StartWithStderrPipe(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>>()))
            .Do(_ => callOrder.Add("launch"));

        CreateSut().LaunchInstances(1);

        Assert.Equal(["init", "launch"], callOrder);
    }
}
