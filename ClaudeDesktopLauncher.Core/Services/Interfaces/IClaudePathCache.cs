namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Caches the resolved Claude exe path and refreshes it on demand.
/// </summary>
public interface IClaudePathCache
{
    string? GetPath();
    void Refresh();
    bool IsClaudeInstalled();
}
