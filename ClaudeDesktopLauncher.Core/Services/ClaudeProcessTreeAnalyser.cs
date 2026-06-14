namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Pure-function helpers for analysing Electron/Claude process trees.
/// All methods are static and side-effect free so they can be used
/// by both the scanner and in unit tests without any OS dependencies.
///
/// <para>
/// Claude Desktop is an Electron application. Each visible window
/// ("main" process) spawns roughly 10-15 child processes: renderer,
/// GPU, crashpad handler, audio/video utility, network service, and
/// node-service. Task Manager's per-app RAM/CPU figures sum the entire
/// tree; CoODL's per-slot cards should match that total.
/// </para>
///
/// Extracted from <see cref="WmiClaudeProcessScanner"/> in v1.10.9
/// to keep the scanner under 200 lines and to make the tree-walking
/// logic unit-testable independently of WMI.
/// </summary>
public static class ClaudeProcessTreeAnalyser
{
    /// <summary>
    /// Collects all descendant PIDs of <paramref name="rootPid"/> via
    /// BFS over the parent-to-children mapping. The root itself is
    /// <b>not</b> included in the result — only its descendants are.
    ///
    /// <para>
    /// Handles arbitrary depth (renderer processes can spawn their own
    /// child utilities). BFS rather than DFS avoids stack overflows on
    /// pathological trees and is simpler to follow.
    /// </para>
    ///
    /// <para>
    /// Returns an empty list when <paramref name="rootPid"/> has no
    /// children in the map (i.e. the process is a leaf node or its
    /// children were not captured in the WMI snapshot).
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> CollectDescendantPids(
        int rootPid,
        IReadOnlyDictionary<int, IReadOnlyList<int>> childrenByParent)
    {
        var result = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children)) continue;
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }
        return result;
    }
}
