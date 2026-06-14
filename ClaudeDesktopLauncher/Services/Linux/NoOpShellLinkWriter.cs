using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IShellLinkWriter: always returns false. Only used by
/// IShortcutHealer which is also a no-op on Linux, so this stub is
/// effectively unreachable in practice but satisfies the DI graph.
/// </summary>
public class NoOpShellLinkWriter : IShellLinkWriter
{
    public bool TryCreateShortcut(string lnkPath, string targetExePath, string description) => false;
}
