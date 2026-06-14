using System.Reflection;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Reads the application version from the executing assembly.
/// Automatically reflects the version set in the .csproj Version property.
/// </summary>
public class VersionProvider : IVersionProvider
{
    public string GetVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is null) return "?.?.?";
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
