using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services;
using ClaudeDesktopLauncher.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Exercises <see cref="WindowsIconCacheRefresher"/> - the thin
/// wrapper that invokes <c>ie4uinit.exe -show</c> via
/// <see cref="IProcessService"/> to refresh Explorer's icon cache
/// after a shortcut heal.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsIconCacheRefresherTests
{
    private readonly IProcessService _processService = Substitute.For<IProcessService>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    [Fact]
    public void RefreshShortcutIcons_InvokesIe4uinitWithShowFlag()
    {
        var sut = new WindowsIconCacheRefresher(_processService, _logger);

        sut.RefreshShortcutIcons();

        _processService.Received(1).Start("ie4uinit.exe", "-show", false);
    }

    [Fact]
    public void RefreshShortcutIcons_WhenProcessStartThrows_LogsWarningAndReturnsNormally()
    {
        _processService.When(x => x.Start("ie4uinit.exe", Arg.Any<string>(), Arg.Any<bool>()))
            .Throw(new InvalidOperationException("process manager denied request"));
        var sut = new WindowsIconCacheRefresher(_processService, _logger);

        // Must not throw; caller (the shortcut healer) relies on this
        // to be safe after successful shortcut creation.
        var act = () => sut.RefreshShortcutIcons();

        act.Should();
        _logger.Received().LogWarning(
            Arg.Is<string>(s => s.Contains("process manager denied request")),
            Arg.Any<string>());
    }
}

/// <summary>Small test-only extension so the exception-safety test
/// reads naturally. Equivalent to <c>Assert.Null(Record.Exception(act))</c>
/// but more readable at the call site.</summary>
internal static class ActExtensions
{
    public static void Should(this Action act)
    {
        var ex = Record.Exception(act);
        Assert.Null(ex);
    }
}
