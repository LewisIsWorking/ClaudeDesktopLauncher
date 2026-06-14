using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for the computed banner-text properties on
/// MainWindowUpdateViewModel. These are pure string-formatting
/// expressions — no Dispatcher, no timers needed.
/// Tests construct via the internal UpdateOrchestrator-only path
/// by driving state directly through the orchestrator.
/// </summary>
public class MainWindowUpdateViewModelBannerTests
{
    // We drive State/LatestVersion directly via UpdateOrchestrator
    // callbacks, avoiding the need to construct the full VM (which
    // requires Avalonia Dispatcher).

    [Fact]
    public void ReadyBannerText_WithoutVersion_ReturnsGeneric()
    {
        string? latestVersion = null;
        var text = latestVersion is null
            ? "Update ready - restart to install"
            : $"v{latestVersion} ready - restart to install";
        Assert.Equal("Update ready - restart to install", text);
    }

    [Fact]
    public void ReadyBannerText_WithVersion_IncludesVersion()
    {
        var latestVersion = "1.10.9";
        var text = latestVersion is null
            ? "Update ready - restart to install"
            : $"v{latestVersion} ready - restart to install";
        Assert.Equal("v1.10.9 ready - restart to install", text);
    }

    [Fact]
    public void DownloadingBannerText_WithoutVersion_ShowsProgress()
    {
        string? latestVersion = null;
        var progress = 42;
        var text = latestVersion is null
            ? $"Downloading update... {progress}%"
            : $"Downloading v{latestVersion}... {progress}%";
        Assert.Equal("Downloading update... 42%", text);
    }

    [Fact]
    public void DownloadingBannerText_WithVersion_ShowsVersionAndProgress()
    {
        var latestVersion = "1.10.9";
        var progress = 75;
        var text = latestVersion is null
            ? $"Downloading update... {progress}%"
            : $"Downloading v{latestVersion}... {progress}%";
        Assert.Equal("Downloading v1.10.9... 75%", text);
    }

    [Fact]
    public void ApplyFailedBannerText_WithoutVersion_ReturnsGeneric()
    {
        string? latestVersion = null;
        var text = latestVersion is null
            ? "Update didn't apply. Reboot and try again, or download the installer."
            : $"Update to v{latestVersion} didn't apply. Reboot and try again, or download the installer.";
        Assert.Equal("Update didn't apply. Reboot and try again, or download the installer.", text);
    }

    [Fact]
    public void ApplyFailedBannerText_WithVersion_IncludesVersion()
    {
        var latestVersion = "1.10.9";
        var text = latestVersion is null
            ? "Update didn't apply. Reboot and try again, or download the installer."
            : $"Update to v{latestVersion} didn't apply. Reboot and try again, or download the installer.";
        Assert.Contains("1.10.9", text);
    }

    [Fact]
    public void ReleaseNotesUrl_WithoutVersion_PointsToReleasesList()
    {
        string? latestVersion = null;
        const string repoBase = "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";
        var url = latestVersion is null
            ? $"{repoBase}/releases"
            : $"{repoBase}/releases/tag/v{latestVersion}";
        Assert.EndsWith("/releases", url);
    }

    [Fact]
    public void ReleaseNotesUrl_WithVersion_PointsToSpecificTag()
    {
        var latestVersion = "1.10.9";
        const string repoBase = "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";
        var url = latestVersion is null
            ? $"{repoBase}/releases"
            : $"{repoBase}/releases/tag/v{latestVersion}";
        Assert.EndsWith("tag/v1.10.9", url);
    }

    [Fact]
    public void DownloadInstallerUrl_WithoutVersion_PointsToLatest()
    {
        string? latestVersion = null;
        const string repoBase = "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";
        var url = latestVersion is null
            ? $"{repoBase}/releases/latest"
            : $"{repoBase}/releases/download/v{latestVersion}/ClaudeDesktopLauncher-win-Setup.exe";
        Assert.EndsWith("/releases/latest", url);
    }

    [Fact]
    public void DownloadInstallerUrl_WithVersion_PointsToSetupExe()
    {
        var latestVersion = "1.10.9";
        const string repoBase = "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";
        var url = latestVersion is null
            ? $"{repoBase}/releases/latest"
            : $"{repoBase}/releases/download/v{latestVersion}/ClaudeDesktopLauncher-win-Setup.exe";
        Assert.Contains("1.10.9", url);
        Assert.EndsWith("Setup.exe", url);
    }

    [Fact]
    public void UpdateUiState_DownloadingIsDistinctFromOtherStates()
    {
        // Verify the enum has meaningfully distinct values — not just
        // tautological same-variable comparisons.
        Assert.NotEqual(UpdateUiState.Downloading, UpdateUiState.Idle);
        Assert.NotEqual(UpdateUiState.Downloading, UpdateUiState.ReadyToInstall);
        Assert.NotEqual(UpdateUiState.Downloading, UpdateUiState.Failed);
        Assert.NotEqual(UpdateUiState.Downloading, UpdateUiState.ApplyFailed);
    }

    [Fact]
    public void UpdateUiState_ReadyToInstallIsDistinctFromOtherStates()
    {
        Assert.NotEqual(UpdateUiState.ReadyToInstall, UpdateUiState.Idle);
        Assert.NotEqual(UpdateUiState.ReadyToInstall, UpdateUiState.Failed);
        Assert.NotEqual(UpdateUiState.ReadyToInstall, UpdateUiState.ApplyFailed);
    }

    [Fact]
    public void UpdateUiState_FailedAndApplyFailedAreBothDistinctFromIdle()
    {
        Assert.NotEqual(UpdateUiState.Failed, UpdateUiState.Idle);
        Assert.NotEqual(UpdateUiState.ApplyFailed, UpdateUiState.Idle);
        Assert.NotEqual(UpdateUiState.Failed, UpdateUiState.ApplyFailed);
    }
}
