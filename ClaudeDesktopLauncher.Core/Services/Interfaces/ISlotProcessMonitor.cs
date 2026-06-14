using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Event arguments raised when a previously-known launcher-managed slot's
/// main window process is no longer running. Subscribers can use this as
/// a signal that the slot's data directory is becoming unlocked (typically
/// within seconds once Electron helper processes wind down) and that a
/// seed-cache snapshot may now be possible.
/// </summary>
public class SlotClosedEventArgs : EventArgs
{
    public LaunchSlot Slot { get; }
    public SlotClosedEventArgs(LaunchSlot slot) => Slot = slot;
}

/// <summary>
/// Observes <see cref="IProcessService.GetSlotProcesses"/> on a regular
/// interval and raises <see cref="SlotClosed"/> when a slot that was seen
/// running in a previous poll is no longer present. Purely observational -
/// does not itself act on the event; subscribers decide what to do.
/// </summary>
public interface ISlotProcessMonitor
{
    /// <summary>
    /// Raised once per slot closure. Not raised for startup (a slot
    /// appearing for the first time is not a "close"). Never raised for
    /// slots that were never observed running.
    /// </summary>
    event EventHandler<SlotClosedEventArgs>? SlotClosed;

    /// <summary>
    /// Begin polling on the given interval. Safe to call multiple times;
    /// a second Start replaces the previous polling interval.
    /// </summary>
    void Start(TimeSpan pollInterval);

    /// <summary>
    /// Stop polling. No further events will be raised until Start is called again.
    /// </summary>
    void Stop();
}