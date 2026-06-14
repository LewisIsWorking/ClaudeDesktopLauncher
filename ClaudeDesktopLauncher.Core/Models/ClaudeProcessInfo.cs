namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Raw snapshot of a single running claude.exe process as observed via WMI.
/// Contains just enough to classify the process (<see cref="CommandLine"/>)
/// and correlate it with a resource-usage snapshot (<see cref="ProcessId"/>).
///
/// This is the <b>unclassified</b> form; see
/// <see cref="Services.Interfaces.IClaudeProcessClassifier"/> for conversion
/// to <see cref="SlotProcessInfo"/> or <see cref="ExternalProcessInfo"/>.
///
/// <para>
/// <see cref="IsWindowed"/> is true when the process currently has a
/// visible top-level window (<c>MainWindowHandle != 0</c>). False means
/// the process is either an Electron child (renderer / GPU / utility)
/// OR a main process that's been close-to-tray'd - and the scanner
/// filters out Electron children by parent-process identity before this
/// record is even built, so in practice <c>!IsWindowed</c> on a record
/// returned by the scanner means "tray-resident slot main".
/// </para>
///
/// <para>
/// <see cref="ChildProcessIds"/> holds the PIDs of all direct and
/// indirect child claude.exe processes (renderer, GPU, crashpad,
/// network service, node-service, etc.). Used by
/// <see cref="ViewModels.SlotInstanceListViewModel"/> to aggregate
/// RAM and CPU stats across the full Electron process tree so per-slot
/// cards match what Windows Task Manager reports. Added in v1.10.9.
/// </para>
/// </summary>
public record ClaudeProcessInfo(
    int ProcessId,
    string CommandLine,
    DateTime StartTime,
    bool IsWindowed = true,
    IReadOnlyList<int>? ChildProcessIds = null);
