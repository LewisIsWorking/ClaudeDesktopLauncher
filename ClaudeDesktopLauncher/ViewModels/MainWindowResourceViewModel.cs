using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Sub-VM for the resource-monitoring concern of the main window:
/// running instance count, total RAM/CPU, disk usage, the refresh
/// timer that drives both, and the thumbnail capture triggered on
/// every tick.
///
/// <para>
/// <see cref="TotalDiskGb"/> is refreshed asynchronously on
/// construction and on each manual refresh. The scan walks all
/// ClaudeSlot* and ClaudeInstance* directories recursively and
/// can take several minutes on large installs (~133 GB).
/// <see cref="IsDiskScanning"/> is true while the scan is running
/// so the UI can show "Scanning..." instead of a stale figure.
/// </para>
/// </summary>
public partial class MainWindowResourceViewModel : ObservableObject
{
    private readonly IResourceMonitor _resourceMonitor;
    private readonly IWindowThumbnailService _thumbnailService;
    private readonly IClaudeDiskUsageService _diskUsage;
    private readonly SlotInstanceListViewModel _slotInstances;
    private readonly ExternalInstanceListViewModel _externalInstances;
    private readonly Func<bool> _getThumbnailsEnabled;
    private readonly Action _onIntervalChanged;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunningInstances))]
    private int _runningInstanceCount;

    [ObservableProperty] private double _totalRamMb;
    [ObservableProperty] private double _totalCpuPercent;
    [ObservableProperty] private double _totalDiskGb;
    [ObservableProperty] private bool _isDiskScanning;
    [ObservableProperty] private int _intervalSeconds;

    /// <summary>True when at least one Claude process is running.</summary>
    public bool HasRunningInstances => RunningInstanceCount > 0;

    public MainWindowResourceViewModel(
        IResourceMonitor resourceMonitor,
        IWindowThumbnailService thumbnailService,
        IClaudeDiskUsageService diskUsage,
        SlotInstanceListViewModel slotInstances,
        ExternalInstanceListViewModel externalInstances,
        int initialIntervalSeconds,
        Func<bool> getThumbnailsEnabled,
        Action onIntervalChanged)
    {
        _resourceMonitor = resourceMonitor;
        _thumbnailService = thumbnailService;
        _diskUsage = diskUsage;
        _slotInstances = slotInstances;
        _externalInstances = externalInstances;
        _getThumbnailsEnabled = getThumbnailsEnabled;
        _onIntervalChanged = onIntervalChanged;
        _intervalSeconds = initialIntervalSeconds;
        _runningInstanceCount = 0;

        _timer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(_intervalSeconds) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        _ = RefreshDiskUsageAsync();
    }

    /// <summary>
    /// One refresh cycle: re-count running instances, pull the latest
    /// RAM/CPU snapshots from the monitor, push those into both list
    /// VMs, and (if thumbnails are on) capture a fresh frame for every
    /// visible row. Safe to call on-demand from per-row actions.
    /// </summary>
    public void Refresh()
    {
        var snapshots = _resourceMonitor.GetSnapshots();
        TotalRamMb = _resourceMonitor.TotalRamMb;
        TotalCpuPercent = _resourceMonitor.TotalCpuPercent;
        _slotInstances.Refresh(snapshots);
        _externalInstances.Refresh(snapshots);
        RunningInstanceCount = _slotInstances.Items.Count
            + _slotInstances.TrayItems.Count
            + _externalInstances.Items.Count;

        if (_getThumbnailsEnabled())
        {
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, _slotInstances.Items, 240, 150);
            ThumbnailRefresher.RefreshVisibleThumbnails(
                _thumbnailService, _externalInstances.Items, 240, 150);
        }
    }

    /// <summary>
    /// Scans all Claude data directories on a background thread.
    /// Sets <see cref="IsDiskScanning"/> true for the duration so
    /// the UI shows "Scanning..." rather than a stale figure.
    /// On large installs (~133 GB) the scan can take several minutes.
    /// </summary>
    private async Task RefreshDiskUsageAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsDiskScanning = true);
        var gb = await _diskUsage.GetTotalGbAsync().ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TotalDiskGb = gb;
            IsDiskScanning = false;
        });
    }

    partial void OnIntervalSecondsChanged(int value)
    {
        if (value < 1) return;
        _timer.Interval = TimeSpan.FromSeconds(value);
        _onIntervalChanged();
    }

    [RelayCommand]
    private void ManualRefresh()
    {
        Refresh();
        _ = RefreshDiskUsageAsync();
    }
}