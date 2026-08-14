using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Process IDENTITY and resolution failure. Call counting lives in ClaudeProcessWmiQueryTests; these
/// cases are about the cache answering for the wrong process, or inventing an answer it does not
/// have. Both produce a confidently wrong classification rather than a slow one, which is the worse
/// failure of the two.
/// </summary>
[SupportedOSPlatform("windows")]
public class ClaudeProcessWmiQueryEdgeTests
{
    /// <summary>
    /// ⚠️ PID REUSE IS THE ONE FAILURE WORSE THAN THE COST THIS CLASS REMOVES. Windows recycles PIDs,
    /// and a cache keyed on pid alone would serve the previous occupant's command line - a
    /// confidently wrong answer that classifies a slot as the wrong instance. The start time is what
    /// makes the key identify a PROCESS rather than a number.
    /// </summary>
    [Fact]
    public void APidReusedByADifferentProcess_IsReReadNotServedStale()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --original");
        var q = s.Build();
        q.QueryClaudeProcesses();

        // Same pid, different start time: a new process wearing an old number.
        s.Live.Clear();
        s.Live.Add(new LiveClaudeProcess(10, 999));
        s.Known[10] = new ProcessRecord(10, 7, "claude.exe --recycled");
        var result = q.QueryClaudeProcesses();

        Assert.Equal(2, s.WmiCalls);
        Assert.Equal([10], s.Requests[1]);
        Assert.Equal("claude.exe --recycled", result[0].CommandLine);
        Assert.Equal(7, result[0].ParentPid);
    }

    /// <summary>
    /// The source may return a pid we never asked about - it started between listing and reading.
    /// Caching it would put an entry in with a start time we never observed, so the reuse guard above
    /// could not fire for it. It is dropped and picked up on the next poll instead.
    /// </summary>
    [Fact]
    public void ARecordForAnUnrequestedPid_IsNotCached()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Add(10, 111, 1, "claude.exe --a");
        var q = new ClaudeProcessWmiQuery(
            s.ListLive,
            pids =>
            {
                s.Requests.Add([.. pids]);
                return
                [
                    new ProcessRecord(10, 1, "claude.exe --a"),
                    new ProcessRecord(99, 10, "claude.exe --late")
                ];
            });

        var result = q.QueryClaudeProcesses();

        Assert.Single(result);
        Assert.Equal(10, result[0].Pid);
    }

    /// <summary>
    /// A pid WMI could not resolve must not appear with an empty command line - it would classify as
    /// an external Claude rather than a slot. It is simply absent until a later poll resolves it.
    /// </summary>
    [Fact]
    public void AnUnresolvablePid_IsOmittedRatherThanInvented()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Live.Add(new LiveClaudeProcess(10, 111));   // live, but absent from Known
        var q = s.Build();

        var result = q.QueryClaudeProcesses();

        Assert.Empty(result);
        Assert.Equal(1, s.WmiCalls);
    }

    [Fact]
    public void AnUnresolvedPid_IsRetriedOnTheNextPoll()
    {
        var s = new ClaudeProcessWmiQueryTestSource();
        s.Live.Add(new LiveClaudeProcess(10, 111));
        var q = s.Build();
        q.QueryClaudeProcesses();

        s.Known[10] = new ProcessRecord(10, 1, "claude.exe --resolved-late");
        var result = q.QueryClaudeProcesses();

        Assert.Equal(2, s.WmiCalls);
        Assert.Single(result);
        Assert.Equal("claude.exe --resolved-late", result[0].CommandLine);
    }
}
