namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Represents a named persistent slot for a Claude Desktop instance.
/// Fixed slot names ensure login sessions are preserved between launches.
/// </summary>
public record LaunchSlot
{
    public int SlotNumber { get; init; }
    public string Name => $"Claude Slot {SlotNumber}";
    public string DataDirectoryName => $"ClaudeSlot{SlotNumber}";

    public LaunchSlot(int slotNumber)
    {
        if (slotNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "Slot number must be 1 or greater.");

        SlotNumber = slotNumber;
    }
}
