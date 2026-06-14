namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Links a running Claude process (by PID) to the launcher-managed slot number
/// it is using, determined by inspecting the process commandline for a
/// <c>--user-data-dir="...\ClaudeSlotN"</c> flag.
/// Used by <see cref="Services.Interfaces.ISlotManager"/> to decide which slot
/// numbers are currently occupied, and by the resource monitor to classify
/// processes as launcher-managed vs external.
///
/// <para>
/// <see cref="IsTrayResident"/> is true when the slot's main process is
/// alive but its window is currently hidden (close-to-tray state). The
/// UI renders tray-resident slots in a separate "Hidden / tray" section
/// because the user can't click into them from the taskbar and needs
/// the launcher to surface them explicitly.
/// </para>
/// </summary>
public record SlotProcessInfo(
    int ProcessId,
    int SlotNumber,
    bool IsTrayResident = false);
