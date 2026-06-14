using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Caches the resolved Claude exe path and refreshes it on demand.
/// Ensures Claude updates don't break the launcher without user intervention.
/// </summary>
public class ClaudePathCache : IClaudePathCache
{
    private readonly IClaudePathResolver _resolver;
    private CachedClaudePath? _cache;

    public ClaudePathCache(IClaudePathResolver resolver)
    {
        _resolver = resolver;
    }

    public string? GetPath()
    {
        if (_cache is null || _cache.IsStale)
            Refresh();

        return _cache?.ExePath;
    }

    public void Refresh()
    {
        var path = _resolver.ResolveClaudeExePath();
        _cache = new CachedClaudePath(path, DateTime.UtcNow);
    }

    public bool IsClaudeInstalled() => GetPath() is not null;
}
