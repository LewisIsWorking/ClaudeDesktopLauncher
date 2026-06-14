using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Seeds a Claude slot with login credentials from the default Claude profile
/// on first use, so users do not need to log in to every slot separately.
/// </summary>
public interface ISlotInitialiser
{
    /// <summary>
    /// Ensures the slot has login credentials. If the slot has never been used
    /// (or has an empty cookie database), copies cookies from the default Claude
    /// profile. Safe to call every launch � skips if already seeded.
    /// </summary>
    void EnsureInitialised(LaunchSlot slot);
    bool IsSeeded(LaunchSlot slot);
}
