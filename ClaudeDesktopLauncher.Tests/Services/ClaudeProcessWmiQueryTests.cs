using System.Runtime.Versioning;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// How OFTEN the expensive source is consulted, and WHICH pids each call names.
///
/// 2026-08-14: these exist because the class carried its central claim in a comment and nothing
/// else. "In steady state that is zero WMI queries" was written on 2026-08-11 and was never checked
/// by anything, in a class whose entire reason for existing is how rarely it calls WMI.
///
/// The cost when it is wrong is not theoretical. Polling Win32_Process for command lines costs ~1.45
/// CPU-seconds per call inside the WmiPrvSE host; a host observed on 2026-08-14 had accumulated 22.0
/// hours of CPU in 41.9 hours of life and was burning ~1.8 cores. A regression here is invisible in
/// the UI, invisible in the logs, and shows up only as a machine that is quietly slower.
///
/// So the assertions COUNT THE CALLS. Anything less would pass just as happily against the original
/// implementation that queried every single poll.
///
/// ⚠️ MUTATION-PROVEN 2026-08-14, and the result is worth recording. Adding `_cache.Clear()` to the
/// top of QueryClaudeProcesses - which is precisely the old behaviour - fails 3 of these 11:
/// SteadyState_AsksWmiExactlyOnce, ANewProcess_IsTheOnlyThingAskedAbout, AProcessExiting_CostsNoQuery.
///
/// SteadyState_StillReturnsFullResults_FromCache PASSES under that mutation, because it asserts the
/// ANSWER and the answer is identical either way. That is the whole trap: a suite written the obvious
/// way, checking returned data, would have gone green against an implementation burning 1.8 cores.
///
/// Process-identity cases (pid reuse, unresolvable pids) live in ClaudeProcessWmiQueryEdgeTests.
/// </summary>
/// <remarks>
/// Marked windows-only to match the class under test, not to silence CA1416. The csproj already
/// excludes this file from the Linux build for the same reason the Core project excludes
/// ClaudeProcessWmiQuery.cs, so the attribute states a fact the build already enforces rather than
/// making a promise nothing checks.
/// </remarks>
[SupportedOSPlatform("windows")]
public class ClaudeProcessWmiQueryTests
{
    [Fact]
    public void FirstCall_ReadsEveryLivePid()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        s.Add(11, 222, 10, "claude.exe --renderer");
        var q = s.Build();

        var result = q.QueryClaudeProcesses();

        Assert.Equal(1, s.WmiCalls);
        Assert.Equal([10, 11], s.Requests[0]);
        Assert.Equal(2, result.Count);
        Assert.Equal("claude.exe --a", result[0].CommandLine);
        Assert.Equal(10, result[1].ParentPid);
    }

    /// <summary>
    /// ⚠️ THE HEADLINE. Nothing changed, so nothing is asked. This is the assertion the original
    /// implementation fails: it queried on every poll, every two seconds, forever.
    /// </summary>
    [Fact]
    public void SteadyState_AsksWmiExactlyOnce_NoMatterHowManyPolls()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        s.Add(11, 222, 10, "claude.exe --renderer");
        var q = s.Build();

        for (var i = 0; i < 50; i++) { q.QueryClaudeProcesses(); }

        Assert.Equal(1, s.WmiCalls);
    }

    [Fact]
    public void SteadyState_StillReturnsFullResults_FromCache()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        var q = s.Build();
        q.QueryClaudeProcesses();

        var second = q.QueryClaudeProcesses();

        Assert.Single(second);
        Assert.Equal("claude.exe --a", second[0].CommandLine);
        Assert.Equal(1, second[0].ParentPid);
    }

    [Fact]
    public void ANewProcess_IsTheOnlyThingAskedAbout()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        var q = s.Build();
        q.QueryClaudeProcesses();

        s.Add(12, 333, 10, "claude.exe --gpu");
        var result = q.QueryClaudeProcesses();

        Assert.Equal(2, s.WmiCalls);
        Assert.Equal([12], s.Requests[1]);   // NOT [10, 12]
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AProcessExiting_CostsNoQuery()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        s.Add(11, 222, 10, "claude.exe --renderer");
        var q = s.Build();
        q.QueryClaudeProcesses();

        s.Live.RemoveAll(p => p.Pid == 11);
        var result = q.QueryClaudeProcesses();

        Assert.Equal(1, s.WmiCalls);
        Assert.Single(result);
        Assert.Equal(10, result[0].Pid);
    }

    [Fact]
    public void NoClaudeRunning_ReturnsEmptyAndAsksNothing()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        var q = s.Build();

        var result = q.QueryClaudeProcesses();

        Assert.Empty(result);
        Assert.Equal(0, s.WmiCalls);
    }

    [Fact]
    public void ClosingClaudeEntirely_ClearsTheCache_SoARestartIsReRead()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        var q = s.Build();
        q.QueryClaudeProcesses();

        s.Live.Clear();
        q.QueryClaudeProcesses();          // everything gone

        s.Live.Add(new Core.Services.LiveClaudeProcess(10, 111));   // same pid AND same ticks, deliberately
        var result = q.QueryClaudeProcesses();

        Assert.Equal(2, s.WmiCalls);
        Assert.Single(result);
    }
}
