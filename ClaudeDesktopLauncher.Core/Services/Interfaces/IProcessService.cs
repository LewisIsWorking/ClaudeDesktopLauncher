using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts process operations to allow unit testing without spawning real processes.
/// </summary>
public interface IProcessService
{
    void Start(string fileName, string? arguments = null, bool useShellExecute = false);

    /// <summary>
    /// Starts a process with its stderr stream redirected to
    /// <paramref name="onStderrLine"/>, invoked once per line as the child
    /// process writes. Used specifically for Claude Desktop launches so
    /// upstream Electron/Node warnings are captured into CoODL's
    /// diagnostic log rather than being lost or cluttering the terminal.
    /// Returns the child process ID for log correlation, or 0 if the
    /// process failed to start.
    /// </summary>
    int StartWithStderrPipe(
        string fileName,
        string? arguments,
        Action<string> onStderrLine);

    int CountByName(string processName);

    /// <summary>
    /// Counts only processes with a visible main window.
    /// Use this for Electron apps that spawn many background child processes.
    /// </summary>
    int CountByNameWithWindow(string processName);

    /// <summary>
    /// Returns raw snapshots for all windowed processes with the given name.
    /// Excludes child processes (renderer, GPU, crashpad, etc.) that have no
    /// visible window. Used where only the browser-main process matters.
    /// </summary>
    IReadOnlyList<ProcessSnapshot> GetWindowedProcessSnapshots(string processName);

    /// <summary>
    /// Returns raw snapshots for ALL processes with the given name,
    /// including child/helper processes that have no visible window.
    /// Used by <see cref="ResourceMonitor"/> so that Total RAM and CPU
    /// match what Windows Task Manager reports for the full process tree
    /// rather than just the browser-main process per slot.
    /// Added in v1.10.7 to fix the underestimation of resource totals.
    /// </summary>
    IReadOnlyList<ProcessSnapshot> GetAllProcessSnapshots(string processName);

    /// <summary>
    /// Terminates the process with the given ID, including all its child processes.
    /// </summary>
    void KillProcess(int processId);
}
