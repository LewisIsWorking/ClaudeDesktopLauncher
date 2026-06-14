namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Manages whether the launcher starts automatically with Windows.
/// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run registry key.
/// </summary>
public interface IStartupService
{
    bool IsStartupEnabled();
    void EnableStartup(string executablePath);
    void DisableStartup();
}
