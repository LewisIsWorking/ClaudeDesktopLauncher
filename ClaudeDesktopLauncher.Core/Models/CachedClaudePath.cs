namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// Holds a resolved Claude exe path and the time it was cached.
/// Considered stale after 24 hours to prompt a refresh on next app launch.
/// </summary>
public record CachedClaudePath(string? ExePath, DateTime CachedAt)
{
    private static readonly TimeSpan StaleDuration = TimeSpan.FromHours(24);

    public bool IsStale => DateTime.UtcNow - CachedAt > StaleDuration;
}
