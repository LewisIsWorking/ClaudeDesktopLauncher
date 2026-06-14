namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Manages the system tray icon lifecycle.
/// </summary>
public interface ITrayIconService
{
    void Initialise(Action onShow, Action onLaunchClaude, Action onQuit);
    void Dispose();
}
