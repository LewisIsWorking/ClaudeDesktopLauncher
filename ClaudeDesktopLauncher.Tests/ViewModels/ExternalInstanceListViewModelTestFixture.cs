using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Shared mock graph and helpers for <c>ExternalInstanceListViewModel</c>
/// tests. Extracted so the individual refresh-behaviour and
/// close-dialog-behaviour test files can stay under the 200-line limit
/// without either duplicating NSubstitute wiring or fighting over which
/// file owns <see cref="Claude"/>-style factory helpers.
/// </summary>
public class ExternalInstanceListViewModelTestFixture
{
    public IClaudeProcessScanner Scanner { get; } = Substitute.For<IClaudeProcessScanner>();
    public IClaudeProcessClassifier Classifier { get; } = Substitute.For<IClaudeProcessClassifier>();
    public IConfirmDialogService ConfirmDialog { get; } = Substitute.For<IConfirmDialogService>();
    public IProcessService ProcessService { get; } = Substitute.For<IProcessService>();
    public ILoggingService Logger { get; } = Substitute.For<ILoggingService>();

    public ExternalInstanceListViewModel CreateSut() =>
        new(Scanner, Classifier, ConfirmDialog, ProcessService, Logger);

    public static ClaudeProcessInfo Claude(int pid, string cmdLine = "") =>
        new(pid, cmdLine, DateTime.UtcNow);

    public static ExternalProcessInfo External(int pid, string cmdLine = "claude.exe") =>
        new(pid, cmdLine, DateTime.UtcNow);

    public static InstanceResourceSnapshot Snap(
        int pid,
        double cpu = 0,
        double ramMb = 0,
        TimeSpan uptime = default) =>
        new(pid, 0, cpu, (long)(ramMb * 1024 * 1024), uptime);
}
