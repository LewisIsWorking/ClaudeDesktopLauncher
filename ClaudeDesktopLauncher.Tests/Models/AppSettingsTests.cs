using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void GetSlotName_WhenNameSet_ReturnsName()
    {
        var settings = new AppSettings();
        settings.SlotNames[1] = "Work";

        Assert.Equal("Work", settings.GetSlotName(1));
    }

    [Fact]
    public void GetSlotName_WhenNoNameSet_ReturnsDefaultName()
    {
        var settings = new AppSettings();

        Assert.Equal("Instance 3", settings.GetSlotName(3));
    }

    [Fact]
    public void GetSlotName_WhenNameIsWhitespace_ReturnsDefaultName()
    {
        var settings = new AppSettings();
        settings.SlotNames[1] = "   ";

        Assert.Equal("Instance 1", settings.GetSlotName(1));
    }

    [Fact]
    public void GetSlotName_WhenNameIsEmpty_ReturnsDefaultName()
    {
        var settings = new AppSettings();
        settings.SlotNames[2] = string.Empty;

        Assert.Equal("Instance 2", settings.GetSlotName(2));
    }
}
