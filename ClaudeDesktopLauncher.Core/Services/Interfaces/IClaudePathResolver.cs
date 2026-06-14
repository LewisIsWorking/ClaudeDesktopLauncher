namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Resolves the Claude Desktop executable path on the current platform.
/// </summary>
public interface IClaudePathResolver
{
    string? ResolveClaudeExePath();
    bool IsClaudeInstalled();
}
