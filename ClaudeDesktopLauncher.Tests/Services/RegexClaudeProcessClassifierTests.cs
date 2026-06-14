using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

public class RegexClaudeProcessClassifierTests
{
    private readonly RegexClaudeProcessClassifier _sut = new();

    private static ClaudeProcessInfo Claude(string cmdLine, int pid = 100) =>
        new(pid, cmdLine, DateTime.UtcNow);

    [Fact]
    public void TryClassifyAsSlot_WithQuotedSlotPath_ReturnsSlotInfo()
    {
        var info = Claude(@"""C:\Program Files\WindowsApps\Claude\app\claude.exe"" --user-data-dir=""C:\\Users\\TestUser\\AppData\\Local\\ClaudeSlot3""");

        var result = _sut.TryClassifyAsSlot(info);

        Assert.NotNull(result);
        Assert.Equal(100, result!.ProcessId);
        Assert.Equal(3, result.SlotNumber);
    }

    [Fact]
    public void TryClassifyAsSlot_WithUnquotedSlotPath_ReturnsSlotInfo()
    {
        var info = Claude(@"C:\claude.exe --user-data-dir=C:\\Users\\TestUser\\AppData\\Local\\ClaudeSlot7");

        Assert.Equal(7, _sut.TryClassifyAsSlot(info)?.SlotNumber);
    }

    [Fact]
    public void TryClassifyAsSlot_CaseInsensitive()
    {
        var info = Claude(@"claude.exe --USER-DATA-DIR=c:\path\claudeslot12");

        Assert.Equal(12, _sut.TryClassifyAsSlot(info)?.SlotNumber);
    }

    [Fact]
    public void TryClassifyAsSlot_WithExternalCommandLine_ReturnsNull()
    {
        var info = Claude(@"""C:\Program Files\WindowsApps\Claude\app\claude.exe""");

        Assert.Null(_sut.TryClassifyAsSlot(info));
    }

    [Fact]
    public void TryClassifyAsSlot_WithEmptyCommandLine_ReturnsNull()
    {
        Assert.Null(_sut.TryClassifyAsSlot(Claude("")));
    }

    [Fact]
    public void TryClassifyAsSlot_WithDefaultProfileUserDataDir_ReturnsNull()
    {
        // User may run: claude.exe --user-data-dir=C:\Custom\Path (no ClaudeSlot prefix)
        var info = Claude(@"claude.exe --user-data-dir=C:\Custom\Path");

        Assert.Null(_sut.TryClassifyAsSlot(info));
    }

    [Fact]
    public void TryClassifyAsExternal_WithExternalCommandLine_ReturnsInfo()
    {
        var info = Claude(@"""C:\Program Files\WindowsApps\Claude\app\claude.exe""");

        var result = _sut.TryClassifyAsExternal(info);

        Assert.NotNull(result);
        Assert.Equal(100, result!.ProcessId);
        Assert.Equal(@"""C:\Program Files\WindowsApps\Claude\app\claude.exe""", result.CommandLine);
    }

    [Fact]
    public void TryClassifyAsExternal_WithSlotCommandLine_ReturnsNull()
    {
        var info = Claude(@"claude.exe --user-data-dir=C:\ClaudeSlot3");

        Assert.Null(_sut.TryClassifyAsExternal(info));
    }

    [Fact]
    public void TryClassifyAsExternal_WithEmptyCommandLine_ReturnsExternalWithEmptyCommandLine()
    {
        // Access-denied under WMI returns empty cmdline - surface as external
        // rather than hiding the PID entirely.
        var result = _sut.TryClassifyAsExternal(Claude(""));

        Assert.NotNull(result);
        Assert.Equal(100, result!.ProcessId);
        Assert.Equal(string.Empty, result.CommandLine);
    }

    [Fact]
    public void TryClassifyAsExternal_PreservesStartTime()
    {
        var start = new DateTime(2026, 4, 1, 10, 30, 0, DateTimeKind.Utc);
        var info = new ClaudeProcessInfo(42, @"claude.exe", start);

        Assert.Equal(start, _sut.TryClassifyAsExternal(info)!.StartTime);
    }
    [Fact]
    public void TryClassifyAsSlot_LinuxForwardSlashPath_ReturnsSlotInfo()
    {
        // Real Linux slot launch line: bash launcher + electron + asar + flag.
        var info = Claude("/usr/lib/claude-desktop/node_modules/electron/dist/electron " +
                         "/usr/lib/claude-desktop/node_modules/electron/dist/resources/app.asar " +
                         "--no-sandbox --user-data-dir=/home/pathwars/.config/ClaudeSlot5",
                         pid: 9834);

        var result = _sut.TryClassifyAsSlot(info);

        Assert.NotNull(result);
        Assert.Equal(9834, result!.ProcessId);
        Assert.Equal(5, result.SlotNumber);
    }

    [Fact]
    public void TryClassifyAsSlot_LinuxExternalCmdline_ReturnsNull()
    {
        // The launcher-unaware Claude install (default user-data dir).
        var info = Claude("/usr/lib/claude-desktop/node_modules/electron/dist/electron " +
                         "/usr/lib/claude-desktop/node_modules/electron/dist/resources/app.asar " +
                         "--no-sandbox --ozone-platform=wayland");
        Assert.Null(_sut.TryClassifyAsSlot(info));
    }

}
