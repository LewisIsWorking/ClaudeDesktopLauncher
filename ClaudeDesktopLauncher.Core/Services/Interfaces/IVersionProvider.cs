namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Provides the current application version string.
/// </summary>
public interface IVersionProvider
{
    string GetVersion();
}
