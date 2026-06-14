using ClaudeDesktopLauncher.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for the destructive-close flow on external Claude rows: the
/// confirm dialog is shown with destructive severity, the kill only
/// happens on user confirm, cancel leaves the process alone, and
/// exceptions from the kill are caught and logged (the app must not
/// crash if the user happens to hit the X on a process they don't own).
/// </summary>
public class ExternalInstanceListViewModelCloseTests
{
    private readonly ExternalInstanceListViewModelTestFixture _f = new();

    [Fact]
    public async Task Close_WhenUserConfirms_KillsProcess()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.ConfirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(true);
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<Core.Models.InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _f.ProcessService.Received(1).KillProcess(100);
    }

    [Fact]
    public async Task Close_WhenUserCancels_DoesNotKillProcess()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.ConfirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(false);
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<Core.Models.InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _f.ProcessService.DidNotReceive().KillProcess(Arg.Any<int>());
    }

    [Fact]
    public async Task Close_ConfirmDialogIsDestructiveSeverity()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.ConfirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(false);
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<Core.Models.InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        await _f.ConfirmDialog.Received(1).ConfirmAsync(
            Arg.Is<ConfirmDialogOptions>(o => o.Severity == DialogSeverity.Destructive));
    }

    [Fact]
    public async Task Close_WhenKillThrows_LogsErrorAndDoesNotRethrow()
    {
        var p1 = ExternalInstanceListViewModelTestFixture.Claude(100);
        _f.Scanner.Scan().Returns(new[] { p1 });
        _f.Classifier.TryClassifyAsExternal(p1).Returns(ExternalInstanceListViewModelTestFixture.External(100));
        _f.ConfirmDialog.ConfirmAsync(Arg.Any<ConfirmDialogOptions>()).Returns(true);
        _f.ProcessService.When(s => s.KillProcess(100)).Do(_ => throw new UnauthorizedAccessException("denied"));
        var sut = _f.CreateSut();
        sut.Refresh(Array.Empty<Core.Models.InstanceResourceSnapshot>());

        await sut.Items[0].CloseCommand.ExecuteAsync(null);

        _f.Logger.Received(1).LogError(
            Arg.Is<string>(s => s.Contains("Failed to close") && s.Contains("100")),
            Arg.Any<Exception>(),
            Arg.Any<string>());
    }
}
