using System.Diagnostics;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Production <see cref="IProcessService"/> built on
/// <see cref="System.Diagnostics.Process"/>.
///
/// <para>
/// <see cref="Start"/> performs a fire-and-forget launch without stream
/// redirection (e.g. opening a folder or URL via the shell).
/// </para>
///
/// <para>
/// <see cref="StartWithStderrPipe"/> redirects the child's stderr and
/// pipes each line to the supplied callback, used for Claude slot
/// launches so upstream Electron/Node warnings (DEP0040, DEP0169,
/// BuddyBleTransport, MaxListeners, etc.) flow into CoODL's log
/// rather than being lost (prod, no console) or cluttering the dev
/// terminal. The callback is the caller's concern - this service
/// stays pure process control.
/// </para>
///
/// Slot/external classification of running Claude instances lives in
/// <see cref="IClaudeProcessScanner"/> + <see cref="IClaudeProcessClassifier"/>
/// so classification concerns stay out of the process-control service.
/// </summary>
public class SystemProcessService : IProcessService
{
    public void Start(string fileName, string? arguments = null, bool useShellExecute = false)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = useShellExecute
        });
    }

    public int StartWithStderrPipe(
        string fileName,
        string? arguments,
        Action<string> onStderrLine)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardError = true,
            // stdout is not redirected so we keep the child's default
            // stdio behaviour for stdout - Claude Desktop rarely prints
            // there anyway, and redirecting both can cause deadlocks if
            // one stream blocks while the reader is draining the other.
        };

        var process = Process.Start(psi);
        if (process is null) return 0;

        var pid = process.Id;

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                onStderrLine(e.Data);
        };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => process.Dispose();

        process.BeginErrorReadLine();
        return pid;
    }

    public int CountByName(string processName) =>
        Process.GetProcessesByName(processName).Length;

    public int CountByNameWithWindow(string processName) =>
        Process.GetProcessesByName(processName)
            .Count(p => p.MainWindowHandle != IntPtr.Zero);

    public IReadOnlyList<ProcessSnapshot> GetWindowedProcessSnapshots(string processName)
    {
        var now = DateTime.UtcNow;
        return Process.GetProcessesByName(processName)
            .Where(p => p.MainWindowHandle != IntPtr.Zero)
            .Select(p => new ProcessSnapshot(
                p.Id,
                p.WorkingSet64,
                p.TotalProcessorTime,
                p.StartTime.ToUniversalTime(),
                now))
            .ToList();
    }

    /// <summary>
    /// Returns snapshots for ALL processes with the given name including
    /// child/helper processes that have no visible window (renderer, GPU,
    /// crashpad, network service, etc.). Used by <see cref="ResourceMonitor"/>
    /// so Total RAM and CPU match what Windows Task Manager shows for the
    /// full process tree rather than just the browser-main process per slot.
    /// Guards against races where a child exits between enumeration and
    /// property access. Added in v1.10.7.
    /// </summary>
    public IReadOnlyList<ProcessSnapshot> GetAllProcessSnapshots(string processName)
    {
        var now = DateTime.UtcNow;
        var results = new List<ProcessSnapshot>();
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try
            {
                results.Add(new ProcessSnapshot(
                    p.Id,
                    p.WorkingSet64,
                    p.TotalProcessorTime,
                    p.StartTime.ToUniversalTime(),
                    now));
            }
            catch (Exception)
            {
                // Process exited between enumeration and property read — skip it.
            }
            finally
            {
                p.Dispose();
            }
        }
        return results;
    }

    public void KillProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Process already exited - nothing to do
        }
    }
}
