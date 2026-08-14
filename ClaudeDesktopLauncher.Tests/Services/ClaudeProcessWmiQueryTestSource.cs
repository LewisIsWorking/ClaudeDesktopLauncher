using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Shared harness for the <see cref="ClaudeProcessWmiQuery"/> suites. Extracted when the single test
/// file reached 252 lines, past this repo's 200-line limit - which its own
/// <c>FileSizeLimitTests.NoCodeFileExceedsTwoHundredLines</c> caught rather than a human noticing.
/// Split by subject (call counting vs process identity), not by trimming assertions.
/// </summary>
/// <remarks>
/// ⚠️ THE POINT OF THIS CLASS IS <see cref="Requests"/>, not the data it returns. The behaviour under
/// test is how OFTEN the expensive source is consulted and WHICH pids each call names. A harness that
/// only recorded answers would pass identically against the original implementation, which queried
/// WMI on every two-second poll and cost ~1.45 CPU-seconds each time.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class ClaudeProcessWmiQueryTestSource
{
    public List<LiveClaudeProcess> Live { get; } = [];

    /// <summary>What the expensive source can resolve. A live pid absent here is unresolvable.</summary>
    public Dictionary<int, ProcessRecord> Known { get; } = [];

    /// <summary>One entry per call; each is the exact pid set that call requested.</summary>
    public List<int[]> Requests { get; } = [];

    public int WmiCalls => Requests.Count;

    public IReadOnlyList<LiveClaudeProcess> ListLive() => Live;

    public IReadOnlyList<ProcessRecord> Read(IReadOnlyList<int> pids)
    {
        Requests.Add([.. pids]);
        return [.. pids.Where(Known.ContainsKey).Select(p => Known[p])];
    }

    public ClaudeProcessWmiQuery Build() => new(ListLive, Read);

    /// <summary>Adds a process that is both alive and resolvable - the ordinary case.</summary>
    public void Add(int pid, long ticks, int parent, string cmd)
    {
        Live.Add(new LiveClaudeProcess(pid, ticks));
        Known[pid] = new ProcessRecord(pid, parent, cmd);
    }
}
