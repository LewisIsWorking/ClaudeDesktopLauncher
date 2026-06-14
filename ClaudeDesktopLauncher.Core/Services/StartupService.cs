using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Manages Windows startup registration via HKCU Run registry key.
/// Runs minimised to tray on startup � does not open the main window.
/// </summary>
public class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClaudeDesktopLauncher";

    private readonly IRegistryService _registry;

    public StartupService(IRegistryService registry)
    {
        _registry = registry;
    }

    public bool IsStartupEnabled()
    {
        var value = _registry.GetValue(RunKeyPath, AppName);
        return value is not null;
    }

    public void EnableStartup(string executablePath)
    {
        _registry.SetValue(RunKeyPath, AppName, $"\"{executablePath}\" --minimised");
    }

    public void DisableStartup()
    {
        _registry.DeleteValue(RunKeyPath, AppName);
    }
}
