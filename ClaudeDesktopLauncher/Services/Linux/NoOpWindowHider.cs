using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IWindowHider: always returns false (= cannot hide).
/// Hiding another application's window on Wayland requires either
/// compositor-specific protocols or a portal that does not yet
/// universally exist. On X11 it would be doable via xdotool's
/// windowunmap; deferred to a future milestone. For v1.10.19 the
/// Hide button on Linux slot cards is a no-op (the slot stays
/// visible after the button is clicked).
/// </summary>
public class NoOpWindowHider : IWindowHider
{
    public bool TryHide(int processId) => false;
}
