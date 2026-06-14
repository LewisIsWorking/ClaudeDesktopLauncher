using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IWindowShower: always returns false (= cannot show).
/// Counterpart to NoOpWindowHider; the same Wayland-restriction
/// rationale applies. Deferred to a future milestone.
/// </summary>
public class NoOpWindowShower : IWindowShower
{
    public bool TryShow(int processId) => false;
}
