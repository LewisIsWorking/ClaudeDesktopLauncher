namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Resolves the installed Claude Desktop version string for display in the
/// launcher UI, so the user can see at a glance what version of Claude they
/// are running against.
/// </summary>
public interface IClaudeVersionResolver
{
    /// <summary>
    /// Returns the installed Claude Desktop product version (e.g.
    /// <c>"1.3109.0.0"</c>), or <c>null</c> if Claude is not installed or
    /// its version information cannot be read. Never throws for these
    /// expected cases.
    /// </summary>
    string? GetClaudeVersion();
}