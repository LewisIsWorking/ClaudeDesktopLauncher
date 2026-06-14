using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IAutoUpdateService: always reports "no update". Velopack
/// supports Linux but the v1.10.19 MVP ships Linux as a fresh build
/// from GitHub Releases without auto-update infrastructure. A future
/// milestone will integrate either AppImage's built-in update or a
/// Linux Velopack flow; until then the launcher just stays on the
/// installed version and the user updates manually.
/// </summary>
public class NoOpAutoUpdateService : IAutoUpdateService
{
    public Task<UpdateCheckResult> CheckForUpdatesAsync() =>
        Task.FromResult(new UpdateCheckResult(UpdateStatus.NoUpdateAvailable));

    public Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null) =>
        Task.FromResult(false);

    public void ApplyUpdatesAndRestart() { }

    public bool IsFirstRunAfterUpdate => false;
}
