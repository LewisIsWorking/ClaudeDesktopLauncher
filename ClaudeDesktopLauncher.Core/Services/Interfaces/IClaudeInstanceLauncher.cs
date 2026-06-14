using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Launches and terminates Claude Desktop instances.
/// </summary>
public interface IClaudeInstanceLauncher
{
    void LaunchSlot(LaunchSlot slot);
    int GetRunningInstanceCount();

    /// <summary>
    /// Picks the next <paramref name="count"/> free slots via the slot
    /// manager, seeds each one's data directory if needed, launches
    /// Claude with <c>--user-data-dir=...\ClaudeSlotN</c> for each, and
    /// returns the slots that were launched. Centralises the whole
    /// launch sequence here rather than forcing every caller
    /// (MainWindowViewModel, future CLI, future automation) to re-wire
    /// the slot-manager + slot-initialiser + launcher dance.
    /// </summary>
    IReadOnlyList<LaunchSlot> LaunchInstances(int count);

    /// <summary>
    /// Terminates the Claude process with the given process ID.
    /// Used to fully close a Claude instance that would otherwise minimize to the tray.
    /// </summary>
    void KillInstance(int processId);
}
