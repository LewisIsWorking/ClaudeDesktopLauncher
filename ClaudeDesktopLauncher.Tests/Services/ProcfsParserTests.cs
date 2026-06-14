using ClaudeDesktopLauncher.Core.Services.Linux;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProcfsParser"/>. The pure-function parsing
/// logic for /proc fields is platform-agnostic so these tests run on
/// every CI platform; they hold the contract that
/// <see cref="ProcfsClaudeProcessScanner"/> depends on.
///
/// <para>Sample blobs in this file were captured from a real running
/// Claude Desktop on Ubuntu (claude-desktop-debian build) so the
/// fixtures match production input exactly, not invented strings.</para>
/// </summary>
public class ProcfsParserTests
{
    // -------------------------------------------------------------------
    // ParseBootTime
    // -------------------------------------------------------------------

    [Fact]
    public void ParseBootTime_ReturnsBtimeValue()
    {
        var stat = "cpu  100 0 50 200\nbtime 1779713446\nprocesses 12345\n";
        Assert.Equal(1779713446, ProcfsParser.ParseBootTime(stat));
    }

    [Fact]
    public void ParseBootTime_MissingBtime_ReturnsZero()
    {
        Assert.Equal(0, ProcfsParser.ParseBootTime("cpu 1 2 3\nintr 4 5\n"));
    }

    [Fact]
    public void ParseBootTime_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0, ProcfsParser.ParseBootTime(string.Empty));
    }

    // -------------------------------------------------------------------
    // ParseStat
    // -------------------------------------------------------------------

    [Fact]
    public void ParseStat_RealElectronStat_ExtractsPpidAndStarttime()
    {
        // Real stat line from PID 9834 (Lewis's running Claude main).
        // Fields after comm: S(state) 9830(ppid) ... 13368(starttime at idx 21 / 19-after-comm)
        var stat = "9834 (electron) S 9830 9830 6538 0 -1 4194304 12345 0 0 0 100 50 0 0 20 0 34 0 13368 1000000 5000 18446744073709551615 1 1 0 0 0 0 0 0 0 0 0 0 17 4 0 0 0 0 0 0 0 0 0 0 0 0 0\n";
        var parsed = ProcfsParser.ParseStat(stat);
        Assert.NotNull(parsed);
        Assert.Equal(9830, parsed!.Value.Ppid);
        Assert.Equal(13368, parsed.Value.StartTicks);
    }

    [Fact]
    public void ParseStat_CommWithSpaces_StillParses()
    {
        // The comm field can legitimately contain spaces and parens
        // (e.g. process renamed via prctl). ParseStat anchors on the
        // LAST ')' in the line so a name like "(my (weird) name)"
        // doesn't break it.
        var stat = "12345 (my (weird) name) S 99 99 99 0 -1 0 0 0 0 0 0 0 0 0 20 0 1 0 7777 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 17 0 0 0 0 0 0 0 0 0 0 0 0 0 0\n";
        var parsed = ProcfsParser.ParseStat(stat);
        Assert.NotNull(parsed);
        Assert.Equal(99, parsed!.Value.Ppid);
        Assert.Equal(7777, parsed.Value.StartTicks);
    }

    [Fact]
    public void ParseStat_NoClosingParen_ReturnsNull()
    {
        Assert.Null(ProcfsParser.ParseStat("malformed input no parens here"));
    }

    [Fact]
    public void ParseStat_TruncatedAfterComm_ReturnsNull()
    {
        Assert.Null(ProcfsParser.ParseStat("12345 (electron) S"));
    }

    // -------------------------------------------------------------------
    // ParseCmdline
    // -------------------------------------------------------------------

    [Fact]
    public void ParseCmdline_NulSeparatedArgv_BecomesSpaceSeparated()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            "/usr/lib/claude-desktop/node_modules/electron/dist/electron\0--no-sandbox\0--user-data-dir=/home/u/.config/ClaudeSlot1\0");
        var result = ProcfsParser.ParseCmdline(bytes);
        Assert.Equal(
            "/usr/lib/claude-desktop/node_modules/electron/dist/electron --no-sandbox --user-data-dir=/home/u/.config/ClaudeSlot1",
            result);
    }

    [Fact]
    public void ParseCmdline_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ProcfsParser.ParseCmdline(Array.Empty<byte>()));
    }

    // -------------------------------------------------------------------
    // IsMainClaudeElectron
    // -------------------------------------------------------------------

    [Fact]
    public void IsMainClaudeElectron_RealMainCmdline_True()
    {
        // PID 9834 from Lewis's machine - the actual main Claude.
        var cmd = "/usr/lib/claude-desktop/node_modules/electron/dist/electron " +
                  "/usr/lib/claude-desktop/node_modules/electron/dist/resources/app.asar " +
                  "--no-sandbox --enable-features=UseOzonePlatform --ozone-platform=wayland";
        Assert.True(ProcfsParser.IsMainClaudeElectron(cmd));
    }

    [Fact]
    public void IsMainClaudeElectron_ZygoteChild_False()
    {
        // PID 9836/9837 from Lewis's machine - zygote helpers.
        var cmd = "/usr/lib/claude-desktop/node_modules/electron/dist/electron --type=zygote --no-sandbox";
        Assert.False(ProcfsParser.IsMainClaudeElectron(cmd));
    }

    [Fact]
    public void IsMainClaudeElectron_RandomElectronApp_False()
    {
        // Some other Electron app running on the box (e.g. VSCode).
        var cmd = "/snap/code/current/usr/share/code/code --enable-crashpad";
        Assert.False(ProcfsParser.IsMainClaudeElectron(cmd));
    }

    [Fact]
    public void IsMainClaudeElectron_Empty_False()
    {
        Assert.False(ProcfsParser.IsMainClaudeElectron(string.Empty));
    }

    // -------------------------------------------------------------------
    // IsPidDirectoryName
    // -------------------------------------------------------------------

    [Theory]
    [InlineData("1234", true)]
    [InlineData("1", true)]
    [InlineData("sys", false)]
    [InlineData("self", false)]
    [InlineData("thread-self", false)]
    [InlineData("12a3", false)]
    [InlineData("", false)]
    public void IsPidDirectoryName_Cases(string input, bool expected)
    {
        Assert.Equal(expected, ProcfsParser.IsPidDirectoryName(input));
    }
}
