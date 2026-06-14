using System.Runtime.Versioning;
using Microsoft.Win32;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Production registry implementation using Microsoft.Win32.Registry.
/// Windows-only � on other platforms, IRegistryService should be swapped for a no-op.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsRegistryService : IRegistryService
{
    public string? GetValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string keyPath, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(valueName, value);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
