using System.Text.Json;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Persists user preferences to %APPDATA%\ClaudeDesktopLauncher\settings.json.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly string _settingsPath;
    // 🪪 Pre-rename location. The app used to be "ComeOnOverDesktopLauncher"; the clean rename
    //    to ClaudeDesktopLauncher gives it a fresh %APPDATA% folder, so we migrate the old
    //    settings once on first launch rather than silently resetting the user's preferences.
    private readonly string _legacySettingsPath;

    public SettingsService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "ClaudeDesktopLauncher", "settings.json");
        _legacySettingsPath = Path.Combine(appData, "ComeOnOverDesktopLauncher", "settings.json");
    }

    public AppSettings Load()
    {
        // 🔁 One-time migration from the pre-rename folder so the rename doesn't lose preferences.
        if (!_fileSystem.FileExists(_settingsPath) && _fileSystem.FileExists(_legacySettingsPath))
        {
            try
            {
                var legacy = JsonSerializer.Deserialize<AppSettings>(_fileSystem.ReadAllText(_legacySettingsPath));
                if (legacy != null) { Save(legacy); return legacy; }
            }
            catch (JsonException) { /* corrupt legacy file — fall through to fresh defaults */ }
        }

        if (!_fileSystem.FileExists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = _fileSystem.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        _fileSystem.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        _fileSystem.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
