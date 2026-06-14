using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Manages Claude Desktop instance slots.
/// </summary>
public interface ISlotManager
{
    IReadOnlyList<LaunchSlot> GetSlots(int count);
    LaunchSlot GetNextAvailableSlot();

    /// <summary>
    /// Returns the next <paramref name="count"/> slot numbers that are NOT
    /// currently occupied by a running Claude process. Scans upward starting
    /// from slot 1; safety-capped so malformed state can never hang the UI.
    /// Used when the user asks to "open N more instances" so existing slots
    /// are left alone and new launches land in free slot numbers.
    /// </summary>
    IReadOnlyList<LaunchSlot> GetNextFreeSlots(int count);
}