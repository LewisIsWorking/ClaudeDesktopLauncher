using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services.Linux;

/// <summary>
/// Linux no-op IRegistryService: returns null/ignores writes. The only
/// caller is StartupService, which uses the registry to register the
/// launcher to run at user login on Windows. On Linux, run-at-startup
/// is implemented via ~/.config/autostart/*.desktop files (a future
/// LinuxStartupService can replace this stub), so for the v1.10.19
/// Linux MVP "run at startup" is simply unavailable - the toggle in
/// the settings panel will appear off and clicking it has no effect.
/// </summary>
public class NoOpRegistryService : IRegistryService
{
    public string? GetValue(string keyPath, string valueName) => null;
    public void SetValue(string keyPath, string valueName, string value) { }
    public void DeleteValue(string keyPath, string valueName) { }
}
