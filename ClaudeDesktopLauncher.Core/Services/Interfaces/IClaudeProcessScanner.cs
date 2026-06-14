using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Returns a raw snapshot of every running claude.exe process, including
/// its command line and start time. Responsible only for the WMI walk -
/// classification into slot vs external is handled by
/// <see cref="IClaudeProcessClassifier"/>.
///
/// Synchronous by design: the WMI <c>Win32_Process</c> query is itself
/// synchronous, and the typical claude.exe count (under 10) keeps the call
/// in the tens-of-milliseconds range. Callers that must not block the UI
/// thread should wrap invocations in <see cref="System.Threading.Tasks.Task.Run(System.Action)"/>.
/// </summary>
public interface IClaudeProcessScanner
{
    IReadOnlyList<ClaudeProcessInfo> Scan();
}