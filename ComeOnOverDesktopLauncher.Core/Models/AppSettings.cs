namespace ComeOnOverDesktopLauncher.Core.Models;

/// <summary>
/// User preferences persisted to disk between sessions.
/// </summary>
public class AppSettings
{
    public int DefaultSlotCount { get; set; } = 3;
    public bool LaunchOnStartup { get; set; } = false;
    public int ResourceRefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// When true, the launcher captures live thumbnails of each running
    /// Claude window on every poll tick and displays them alongside the
    /// slot's stats. On-by-default so the feature is discoverable;
    /// users who prefer a minimal launcher or have privacy concerns can
    /// toggle it off via the "Show thumbnails" checkbox. Thumbnails are
    /// kept in memory only (never written to disk).
    /// </summary>
    public bool ThumbnailsEnabled { get; set; } = true;

    /// <summary>
    /// When true, the launcher's poll tick periodically checks for a
    /// new release via <see cref="Services.Interfaces.IAutoUpdateService"/>
    /// and silently downloads any available update in the background.
    /// On-by-default because auto-update is the whole point of the
    /// v1.10.0 Velopack switch; the user's only opt-out is this
    /// checkbox. Persisted because users who disable it expect that
    /// choice to stick across restarts.
    /// </summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    /// <summary>
    /// When true, the Claude usage dashboard WebView is docked to the
    /// left of the launcher content instead of the right. Default is
    /// false (right). Persisted so the user's preference survives
    /// restarts. Toggled via the "Usage on left" checkbox in the
    /// settings row or via the GridSplitter right-click context menu.
    /// Added in v1.10.7.
    /// </summary>
    public bool UsagePanelOnLeft { get; set; } = false;

    /// <summary>
    /// User-defined names for each slot. Key = slot number, value = display name.
    /// Falls back to "Instance N" when no name is set.
    /// </summary>
    public Dictionary<int, string> SlotNames { get; set; } = new();

    public string GetSlotName(int slotNumber) =>
        SlotNames.TryGetValue(slotNumber, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Instance {slotNumber}";
}
