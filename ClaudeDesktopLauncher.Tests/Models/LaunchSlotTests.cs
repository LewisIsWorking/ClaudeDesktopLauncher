using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Tests.Models;

public class LaunchSlotTests
{
    [Fact]
    public void Constructor_WhenSlotNumberIsValid_SetsProperties()
    {
        var slot = new LaunchSlot(3);

        Assert.Equal(3, slot.SlotNumber);
        Assert.Equal("Claude Slot 3", slot.Name);
        Assert.Equal("ClaudeSlot3", slot.DataDirectoryName);
    }

    [Fact]
    public void Constructor_WhenSlotNumberIsZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaunchSlot(0));
    }

    [Fact]
    public void Constructor_WhenSlotNumberIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaunchSlot(-1));
    }
}
