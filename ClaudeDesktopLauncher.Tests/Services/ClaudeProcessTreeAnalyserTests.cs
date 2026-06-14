using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudeProcessTreeAnalyserTests
{
    private static IReadOnlyDictionary<int, IReadOnlyList<int>> Map(
        params (int parent, int[] children)[] entries) =>
        entries.ToDictionary(e => e.parent, e => (IReadOnlyList<int>)e.children.ToList());

    [Fact]
    public void CollectDescendantPids_WhenNoChildren_ReturnsEmpty()
    {
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, Map());
        Assert.Empty(result);
    }

    [Fact]
    public void CollectDescendantPids_DirectChildrenOnly_ReturnsAllChildren()
    {
        var map = Map((1, [10, 11, 12]));
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, map);
        Assert.Equal(new[] { 10, 11, 12 }, result.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void CollectDescendantPids_MultiLevel_ReturnsAllDescendants()
    {
        // 1 -> 10, 11; 10 -> 100, 101
        var map = Map((1, [10, 11]), (10, [100, 101]));
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, map);
        Assert.Equal(new[] { 10, 11, 100, 101 }, result.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void CollectDescendantPids_DoesNotIncludeRoot()
    {
        var map = Map((1, [10]));
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, map);
        Assert.DoesNotContain(1, result);
    }

    [Fact]
    public void CollectDescendantPids_UnrelatedProcesses_NotIncluded()
    {
        var map = Map((1, [10]), (99, [999]));
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, map);
        Assert.DoesNotContain(999, result);
    }

    [Fact]
    public void CollectDescendantPids_ThreeDeepTree_ReturnsAll()
    {
        // 1 -> 10 -> 100 -> 1000
        var map = Map((1, [10]), (10, [100]), (100, [1000]));
        var result = ClaudeProcessTreeAnalyser.CollectDescendantPids(1, map);
        Assert.Equal(new[] { 10, 100, 1000 }, result.OrderBy(x => x).ToArray());
    }
}
