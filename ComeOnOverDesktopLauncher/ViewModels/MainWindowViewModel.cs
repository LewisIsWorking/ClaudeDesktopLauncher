using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances, startup toggle, and the
/// Logs command.
///
/// <para>
/// Delegates per-concern state to four sub-VMs so this root stays
/// well under the 200-line limit:
/// <list type="bullet">
///   <item><see cref="SlotInstances"/> - launcher-managed slot rows</item>
///   <item><see cref="ExternalInstances"/> - externally-launched Claude rows</item>
///   <item><see cref="Update"/> - Velopack update banner state</item>
///   <item><see cref="Resources"/> - running count, total RAM/CPU, refresh timer, thumbnail capture</item>
/// </list>
/// </para>
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;
    private AppSettings _settings;

    [ObservableProperty] private int _slotCount;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isClaudeInstalled;
    [ObservableProperty] private bool _launchOnStartup;
    [ObservableProperty] private bool _thumbnailsEnabled;

    /// <summary>
    /// When true, the usage WebView is docked to the left of the
    /// launcher content. Persisted in <see cref="AppSettings"/>.
    /// Toggled via the settings-row checkbox (A) or the GridSplitter
    /// right-click context menu (B). Added in v1.10.7.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsagePanelPositionMenuText))]
    private bool _usagePanelOnLeft;

    /// <summary>
    /// Label for the GridSplitter context-menu item. Reads "Move usage
    /// panel to left" when the panel is currently on the right, and
    /// vice versa, so the item always describes what clicking it will do.
    /// </summary>
    public string UsagePanelPositionMenuText => UsagePanelOnLeft
        ? "Move usage panel to right"
        : "Move usage panel to left";

    public string AppVersion { get; }
    public string? ClaudeVersion { get; }
    public string FooterVersionText =>
        ClaudeVersion is null ? AppVersion : $"{AppVersion} - Claude {ClaudeVersion}";
    public SlotInstanceListViewModel SlotInstances { get; }
    public ExternalInstanceListViewModel ExternalInstances { get; }
    public MainWindowUpdateViewModel Update { get; }
    public MainWindowResourceViewModel Resources { get; }

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver,
        IResourceMonitor resourceMonitor,
        IStartupService startupService,
        IAutoUpdateService autoUpdateService,
        IUpdateApplyFailureDetector applyFailureDetector,
        IVersionProvider versionProvider,
        IClaudeVersionResolver claudeVersionResolver,
        IProcessService processService,
        IWindowThumbnailService thumbnailService,
        IThumbnailPreviewService previewService,
        IWindowHider windowHider,
        IWindowShower windowShower,
        IClaudeDiskUsageService diskUsage,
        SlotInstanceListViewModel slotInstances,
        ExternalInstanceListViewModel externalInstances,
        ILoggingService logger)
    {
        _launcher = launcher;
        _settingsService = settingsService;
        _startupService = startupService;
        _processService = processService;
        SlotInstances = slotInstances;
        ExternalInstances = externalInstances;
        _logger = logger;

        AppVersion = $"v{versionProvider.GetVersion()}";
        ClaudeVersion = claudeVersionResolver.GetClaudeVersion();
        _settings = _settingsService.Load();
        _slotCount = _settings.DefaultSlotCount;
        _thumbnailsEnabled = _settings.ThumbnailsEnabled;
        _usagePanelOnLeft = _settings.UsagePanelOnLeft;
        _launchOnStartup = _startupService.IsStartupEnabled();
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();

        Resources = new MainWindowResourceViewModel(
            resourceMonitor, thumbnailService, diskUsage,
            SlotInstances, ExternalInstances,
            _settings.ResourceRefreshIntervalSeconds,
            () => ThumbnailsEnabled,
            onIntervalChanged: () =>
            {
                _settings.ResourceRefreshIntervalSeconds = Resources!.IntervalSeconds;
                SaveSettings();
            });

        SlotCallbackBinder.Bind(SlotInstances, _settings, _launcher, windowHider, windowShower, previewService, SaveSettings, Resources.Refresh);
        SlotCallbackBinder.BindExternal(ExternalInstances, previewService);

        Update = new MainWindowUpdateViewModel(
            autoUpdateService, _processService, _logger, _settings,
            applyFailureDetector, SaveSettings);

        _logger.LogInfo($"MainWindowViewModel ready. ClaudeInstalled={_isClaudeInstalled}, SlotCount={_slotCount}");
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        if (value)
            _startupService.EnableStartup(Assembly.GetEntryAssembly()?.Location ?? string.Empty);
        else
            _startupService.DisableStartup();
    }

    partial void OnThumbnailsEnabledChanged(bool value) =>
        ThumbnailRefresher.HandleToggleChange(
            value, _settings, SaveSettings,
            SlotInstances.Items, SlotInstances.TrayItems, ExternalInstances.Items);

    partial void OnUsagePanelOnLeftChanged(bool value)
    {
        _settings.UsagePanelOnLeft = value;
        SaveSettings();
    }

    [RelayCommand]
    private void ToggleUsagePanelPosition() => UsagePanelOnLeft = !UsagePanelOnLeft;

    [RelayCommand]
    private void LaunchInstances()
    {
        _logger.LogInfo($"LaunchInstances command fired - SlotCount={SlotCount}");
        try
        {
            var launched = _launcher.LaunchInstances(SlotCount);
            Resources.Refresh();
            StatusMessage = $"Launched {launched.Count} instance(s). {Resources.RunningInstanceCount} running.";
            _logger.LogInfo($"LaunchInstances finished - {Resources.RunningInstanceCount} running");
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError("LaunchInstances failed", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        _processService.Start("explorer.exe", _logger.GetLogDirectory(), useShellExecute: false);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.DefaultSlotCount = SlotCount;
        _settingsService.Save(_settings);
    }
}
