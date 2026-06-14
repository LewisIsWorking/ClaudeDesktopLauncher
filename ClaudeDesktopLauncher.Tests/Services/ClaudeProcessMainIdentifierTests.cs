using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Tests for the pure-function <see cref="ClaudeProcessMainIdentifier"/>
/// that identifies claude.exe "main" processes (Electron browser
/// process, one per visible window) by excluding any claude.exe whose
/// parent is also a claude.exe. Verifies the Electron-children filter,
/// multi-main support, empty input, and the tray-resident case where
/// a main has no visible window but is still identified as main.
/// </summary>
public class ClaudeProcessMainIdentifierTests
{
    [Fact]
    public void IdentifyMainPids_WhenNoProcesses_ReturnsEmpty()
    {
        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(
            Array.Empty<(int Pid, int ParentPid)>());

        Assert.Empty(result);
    }

    [Fact]
    public void IdentifyMainPids_SingleMainWithNoChildren_IdentifiesMain()
    {
        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(new[]
        {
            (Pid: 100, ParentPid: 8540)  // parent is shell, not claude
        });

        Assert.Equal(new[] { 100 }, result);
    }

    [Fact]
    public void IdentifyMainPids_MainWithElectronChildren_ExcludesChildren()
    {
        // One main (100), ten Electron children with claude main as parent
        var procs = new[]
        {
            (Pid: 100, ParentPid: 8540),    // main
            (Pid: 200, ParentPid: 100),     // renderer
            (Pid: 201, ParentPid: 100),     // GPU
            (Pid: 202, ParentPid: 100),     // crashpad
            (Pid: 203, ParentPid: 100),     // utility
        };

        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(procs);

        Assert.Equal(new[] { 100 }, result);
    }

    [Fact]
    public void IdentifyMainPids_MultipleMains_IdentifiesAll()
    {
        // Three slots open, each with its own process tree
        var procs = new[]
        {
            (Pid: 100, ParentPid: 8540),    // slot 1 main
            (Pid: 110, ParentPid: 100),     // slot 1 child
            (Pid: 200, ParentPid: 8540),    // slot 2 main
            (Pid: 210, ParentPid: 200),     // slot 2 child
            (Pid: 300, ParentPid: 8540),    // slot 3 main
            (Pid: 310, ParentPid: 300),     // slot 3 child
        };

        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(procs);

        Assert.Equal(new[] { 100, 200, 300 }, result.OrderBy(p => p));
    }

    [Fact]
    public void IdentifyMainPids_TrayResidentMain_IsStillIdentifiedAsMain()
    {
        // Close-to-tray'd slot: main still alive, children still there,
        // only difference is the main has MainWindowHandle == 0 (not
        // visible to this test, but that's handled by the scanner).
        var procs = new[]
        {
            (Pid: 100, ParentPid: 8540),    // tray-resident main
            (Pid: 110, ParentPid: 100),     // still-alive renderer
            (Pid: 120, ParentPid: 100),     // still-alive utility
        };

        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(procs);

        Assert.Equal(new[] { 100 }, result);
    }

    [Fact]
    public void IdentifyMainPids_OrphanedChild_WhoseMainIsGone_IsTreatedAsMain()
    {
        // Edge case: if the main has already exited but WMI still
        // reports a child, that child has no parent in the claude.exe
        // set, so it falls through as a "main". Unusual but acceptable -
        // surfaces what would otherwise be a silently-ignored zombie.
        var procs = new[]
        {
            (Pid: 200, ParentPid: 100),  // claude.exe parent 100 is not in our set
        };

        var result = ClaudeProcessMainIdentifier.IdentifyMainPids(procs);

        Assert.Equal(new[] { 200 }, result);
    }
}
