using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IShortcutHealer: always reports SkippedDevBuild. The
/// Windows implementation re-creates a missing Start Menu .lnk after
/// Velopack's apply-update flow drops it. On Linux, distribution
/// shortcuts are handled by the package manager (.desktop files
/// installed under /usr/share/applications) and the launcher does
/// not need to self-heal them. Returning SkippedDevBuild matches the
/// Windows dev-build behaviour (App.OnFrameworkInitializationCompleted
/// just logs the result and moves on).
/// </summary>
public class NoOpShortcutHealer : IShortcutHealer
{
    public ShortcutHealResult HealIfMissing() => ShortcutHealResult.SkippedDevBuild;
}
