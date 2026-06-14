namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Computes the total on-disk footprint of all Claude slot data
/// directories (<c>%LOCALAPPDATA%\ClaudeSlot*</c>).
///
/// <para>
/// Implemented as an async operation because a recursive file-size
/// scan over 10+ GB of Electron profile data can take several
/// seconds — unsuitable for the UI thread or the main refresh tick.
/// </para>
/// </summary>
public interface IClaudeDiskUsageService
{
    /// <summary>
    /// Returns the combined size of all ClaudeSlot* directories in GB,
    /// rounded to one decimal place. Returns 0 if no slots exist or
    /// if the scan fails. Never throws.
    /// </summary>
    Task<double> GetTotalGbAsync();
}