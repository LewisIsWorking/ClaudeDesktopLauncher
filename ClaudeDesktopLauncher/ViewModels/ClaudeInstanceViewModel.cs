using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance's live resource stats and user-defined name.
/// Exposes Kill/Hide/Show commands so the user can manage the window without
/// touching the Claude tray icon.
///
/// <para>
/// <see cref="Thumbnail"/> holds the most recent PNG snapshot of the instance's
/// window, refreshed on every poll tick via <see cref="UpdateThumbnailFromBytes"/>.
/// Null bytes are a no-op so tray-resident slots keep their last frame.
/// </para>
///
/// <para>
/// <see cref="LastActiveDisplay"/> shows the last time the slot's CPU exceeded
/// <see cref="CpuActivityThreshold"/> percent, updated every poll tick.
/// "Idle" until the first spike is observed.
/// </para>
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject, IThumbnailableViewModel
{
    /// <summary>CPU % floor that counts as "active". Electron idle
    /// baseline is ~0-1%; sustained UI work sits above 3%.</summary>
    private const double CpuActivityThreshold = 3.0;

    private readonly Action<int, string>? _onNameChanged;
    private readonly Action<int>? _onKill;
    private readonly Action<int>? _onHide;
    private readonly Action<int>? _onShow;
    private readonly Action<ClaudeInstanceViewModel>? _onShowPreview;

    /// <summary>UTC timestamp of the last poll tick where CPU exceeded
    /// <see cref="CpuActivityThreshold"/>. Null until the first spike.</summary>
    private DateTime? _lastActiveAt;

    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private int _processId;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private string _uptimeDisplay = string.Empty;
    [ObservableProperty] private string _slotName = string.Empty;
    [ObservableProperty] private Bitmap? _thumbnail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginStatusText))]
    [NotifyPropertyChangedFor(nameof(LoginStatusBackground))]
    [NotifyPropertyChangedFor(nameof(LoginStatusForeground))]
    private bool _isSeeded;

    public string LoginStatusText => IsSeeded ? "Logged in" : "Not logged in";

    /// <summary>Pill background bound directly as an <see cref="IBrush"/>
    /// to avoid the string→Color coercion that broke compiled bindings
    /// in v1.9.1.</summary>
    public IBrush LoginStatusBackground => IsSeeded
        ? new SolidColorBrush(Color.Parse("#2E7D32"))
        : new SolidColorBrush(Color.Parse("#5D2F2F"));

    public IBrush LoginStatusForeground => IsSeeded
        ? new SolidColorBrush(Color.Parse("#81C784"))
        : new SolidColorBrush(Color.Parse("#EF9A9A"));

    public string LoginStatusTooltip => IsSeeded
        ? "Logged in"
        : "Not yet logged in - will log in on first launch";

    /// <summary>
    /// Human-readable activity signal derived from the last time this
    /// slot's CPU exceeded <see cref="CpuActivityThreshold"/> percent.
    /// Updated on every <see cref="UpdateFrom"/> call regardless of
    /// whether the threshold was crossed, so the elapsed time keeps
    /// advancing even during idle periods.
    /// </summary>
    public string LastActiveDisplay
    {
        get
        {
            if (_lastActiveAt is null) return "Idle";
            var age = DateTime.UtcNow - _lastActiveAt.Value;
            if (age.TotalSeconds < 30) return "Active now";
            if (age.TotalHours < 1) return $"Active {(int)age.TotalMinutes}m ago";
            return $"Active {(int)age.TotalHours}h {age.Minutes}m ago";
        }
    }

    public ClaudeInstanceViewModel(
        int instanceNumber,
        string initialName,
        bool isSeeded,
        Action<int, string>? onNameChanged = null,
        Action<int>? onKill = null,
        Action<int>? onHide = null,
        Action<int>? onShow = null,
        Action<ClaudeInstanceViewModel>? onShowPreview = null)
    {
        _instanceNumber = instanceNumber;
        _slotName = initialName;
        _isSeeded = isSeeded;
        _onNameChanged = onNameChanged;
        _onKill = onKill;
        _onHide = onHide;
        _onShow = onShow;
        _onShowPreview = onShowPreview;
    }

    partial void OnSlotNameChanged(string value) =>
        _onNameChanged?.Invoke(InstanceNumber, value);

    [RelayCommand]
    private void Kill() => _onKill?.Invoke(ProcessId);

    /// <summary>Hides window to tray without terminating. v1.10.5+.</summary>
    [RelayCommand]
    private void Hide() => _onHide?.Invoke(ProcessId);

    /// <summary>Restores window from tray to foreground. v1.10.6+.</summary>
    [RelayCommand]
    private void Show() => _onShow?.Invoke(ProcessId);

    /// <summary>Opens lightbox preview for current thumbnail.</summary>
    [RelayCommand]
    private void ShowPreview() => _onShowPreview?.Invoke(this);

    /// <summary>
    /// Refreshes live resource fields from the latest poll snapshot.
    /// Stamps <see cref="_lastActiveAt"/> when CPU clears the threshold
    /// and always raises <see cref="LastActiveDisplay"/> so the elapsed
    /// time string advances each tick.
    /// </summary>
    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        ProcessId = snapshot.ProcessId;
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        UptimeDisplay = snapshot.UptimeDisplay;
        if (snapshot.CpuPercent >= CpuActivityThreshold)
            _lastActiveAt = DateTime.UtcNow;
        OnPropertyChanged(nameof(LastActiveDisplay));
    }

    /// <summary>Replaces thumbnail from PNG bytes. Null/empty is a no-op
    /// (keeps last frame for tray-resident slots). Disposes the old bitmap.</summary>
    public void UpdateThumbnailFromBytes(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return;
        var old = Thumbnail;
        using var ms = new MemoryStream(pngBytes);
        Thumbnail = new Bitmap(ms);
        old?.Dispose();
    }

    /// <summary>Blanks the thumbnail and disposes its buffer. Called when
    /// thumbnails are toggled off or the row is removed.</summary>
    public void ClearThumbnail()
    {
        var old = Thumbnail;
        Thumbnail = null;
        old?.Dispose();
    }
}
