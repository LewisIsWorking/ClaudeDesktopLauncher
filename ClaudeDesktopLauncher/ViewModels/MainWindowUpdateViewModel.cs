using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Sub-VM for the v1.10.0+ Velopack-driven auto-update UI. Owns the
/// UpdateOrchestrator, exposes observable state to the update banner
/// in LaunchControlsPanel, and hosts the user-driven update commands.
/// </summary>
public partial class MainWindowUpdateViewModel : ObservableObject
{
    private const string GithubRepoBase =
        "https://github.com/LewisIsWorking/ClaudeDesktopLauncher";

    private readonly UpdateOrchestrator _orchestrator;
    private readonly IProcessService _processService;
    private readonly DispatcherTimer _checkTimer;

    [ObservableProperty] private bool _autoCheckEnabled;
    [ObservableProperty] private UpdateUiState _state;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty] private string? _latestVersion;

    public bool IsDownloading => State == UpdateUiState.Downloading;
    public bool IsReadyToInstall => State == UpdateUiState.ReadyToInstall;
    public bool IsFailed => State == UpdateUiState.Failed;
    public bool IsApplyFailed => State == UpdateUiState.ApplyFailed;

    /// <summary>True when no update activity is in progress. Drives
    /// the visibility of the manual "Check for updates" button.</summary>
    public bool IsIdle => State == UpdateUiState.Idle;

    public string ReadyBannerText => LatestVersion is null
        ? "Update ready - restart to install"
        : $"v{LatestVersion} ready - restart to install";
    public string DownloadingBannerText => LatestVersion is null
        ? $"Downloading update... {DownloadProgress}%"
        : $"Downloading v{LatestVersion}... {DownloadProgress}%";
    public string ApplyFailedBannerText => LatestVersion is null
        ? "Update didn't apply. Reboot and try again, or download the installer."
        : $"Update to v{LatestVersion} didn't apply. Reboot and try again, or download the installer.";
    public string ReleaseNotesUrl => LatestVersion is null
        ? $"{GithubRepoBase}/releases"
        : $"{GithubRepoBase}/releases/tag/v{LatestVersion}";
    public string DownloadInstallerUrl => LatestVersion is null
        ? $"{GithubRepoBase}/releases/latest"
        : $"{GithubRepoBase}/releases/download/v{LatestVersion}/ClaudeDesktopLauncher-win-Setup.exe";

    public MainWindowUpdateViewModel(
        IAutoUpdateService autoUpdateService,
        IProcessService processService,
        ILoggingService logger,
        AppSettings settings,
        IUpdateApplyFailureDetector applyFailureDetector,
        Action onSettingsChanged)
    {
        _processService = processService;
        _autoCheckEnabled = settings.AutoCheckForUpdates;
        _orchestrator = new UpdateOrchestrator(
            autoUpdateService, logger,
            onStateChanged: s => Dispatcher.UIThread.Post(() => State = s),
            onProgressChanged: p => Dispatcher.UIThread.Post(() => DownloadProgress = p),
            onLatestVersionChanged: v => Dispatcher.UIThread.Post(() => LatestVersion = v));

        if (applyFailureDetector.ApplyFailedRecently(TimeSpan.FromMinutes(2)))
            _orchestrator.MarkApplyFailed();

        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _checkTimer.Tick += async (_, _) => await CheckAsync();
        _checkTimer.Start();
        _ = CheckAsync();

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(AutoCheckEnabled)) return;
            settings.AutoCheckForUpdates = AutoCheckEnabled;
            onSettingsChanged();
        };
    }

    partial void OnStateChanged(UpdateUiState value)
    {
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsReadyToInstall));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsApplyFailed));
        OnPropertyChanged(nameof(IsIdle));
    }

    partial void OnLatestVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(ReadyBannerText));
        OnPropertyChanged(nameof(DownloadingBannerText));
        OnPropertyChanged(nameof(ApplyFailedBannerText));
        OnPropertyChanged(nameof(ReleaseNotesUrl));
        OnPropertyChanged(nameof(DownloadInstallerUrl));
    }

    partial void OnDownloadProgressChanged(int value) =>
        OnPropertyChanged(nameof(DownloadingBannerText));

    private async Task CheckAsync() =>
        await _orchestrator.RunCheckAsync(AutoCheckEnabled);

    [RelayCommand]
    private void Restart() => _orchestrator.ApplyAndRestart();

    [RelayCommand]
    private void Retry() => _orchestrator.Retry();

    [RelayCommand]
    private void OpenReleaseNotes() =>
        _processService.Start(ReleaseNotesUrl, string.Empty, useShellExecute: true);

    [RelayCommand]
    private void DownloadInstaller() =>
        _processService.Start(DownloadInstallerUrl, string.Empty, useShellExecute: true);

    /// <summary>Manual update check - always runs regardless of
    /// AutoCheckEnabled. Wired to the "Check for updates" button
    /// visible in the idle state.</summary>
    [RelayCommand]
    private async Task CheckForUpdates() =>
        await _orchestrator.RunCheckAsync(autoCheckEnabled: true);
}