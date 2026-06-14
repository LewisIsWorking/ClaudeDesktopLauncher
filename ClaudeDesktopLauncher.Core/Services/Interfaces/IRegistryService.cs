namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts Windows registry operations to allow unit testing without real registry access.
/// </summary>
public interface IRegistryService
{
    string? GetValue(string keyPath, string valueName);
    void SetValue(string keyPath, string valueName, string value);
    void DeleteValue(string keyPath, string valueName);
}
