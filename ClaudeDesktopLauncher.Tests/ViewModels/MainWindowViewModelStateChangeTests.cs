using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for observable-property setters on <c>MainWindowViewModel</c>
/// that have side effects on external services. Currently just the
/// LaunchOnStartup toggle; other setters (SlotCount, RefreshIntervalSeconds)
/// are pure state and don't need dedicated tests. Kept in its own file
/// so the behaviour category is discoverable at a glance.
/// </summary>
public class MainWindowViewModelStateChangeTests
{
    private readonly MainWindowViewModelTestFixture _f = new();

    [Fact]
    public void LaunchOnStartup_WhenSetToTrue_CallsEnableStartup()
    {
        var sut = _f.CreateSut();
        sut.LaunchOnStartup = true;
        _f.StartupService.Received().EnableStartup(Arg.Any<string>());
    }

    [Fact]
    public void LaunchOnStartup_WhenSetToFalse_CallsDisableStartup()
    {
        _f.StartupService.IsStartupEnabled().Returns(true);
        var sut = _f.CreateSut();
        sut.LaunchOnStartup = false;
        _f.StartupService.Received().DisableStartup();
    }
}
