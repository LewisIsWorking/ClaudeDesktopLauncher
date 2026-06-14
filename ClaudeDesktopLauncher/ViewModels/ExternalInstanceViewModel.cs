using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Per-row view model for a single externally-launched Claude instance -
/// a <c>claude.exe</c> process the launcher did NOT spawn (its command
/// line has no <c>--user-data-dir=...\ClaudeSlotN</c> flag).
///
/// The close flow (confirm dialog, kill process) is intentionally NOT
/// owned here: this view model exposes a <see cref="CloseCommand"/> that
/// delegates to a callback supplied by the parent list view model so
/// per-row instances stay free of service dependencies and remain
/// trivially unit-testable.
/// </summary>
public partial class ExternalInstanceViewModel : ObservableObject, IThumbnailableViewModel
{
    /// <summary>
    /// Longest command-line display before middle-ellipsis truncation.
    /// Chosen to fit comfortably in the launcher's 440px wide default
    /// window without forcing a horizontal scroll bar.
    /// </summary>
    private const int CommandLineDisplayMaxLength = 80;

    private readonly Func<ExternalInstanceViewModel, Task>? _onClose;
    private readonly Action<ExternalInstanceViewModel>? _onShowPreview;

    /// <summary>
    /// Operating-system process id. Stable for the lifetime of this row
    /// (rows are removed from the parent collection when the PID exits).
    /// </summary>
    public int Pid { get; }

    /// <summary>
    /// <see cref="IThumbnailableViewModel.ProcessId"/> alias exposing
    /// <see cref="Pid"/> under the shared interface name. Avoids
    /// renaming the existing <c>Pid</c> property (which is bound from
    /// XAML) while still letting this VM slot into the common
    /// thumbnail pipeline alongside <see cref="ClaudeInstanceViewModel"/>.
    /// </summary>
    public int ProcessId => Pid;

    /// <summary>Full command line as reported by WMI. Shown in tooltips
    /// and in the close-confirmation dialog so the user can decide
    /// whether the process is the one they think it is.</summary>
    public string CommandLine { get; }

    /// <summary>Smart-trimmed command line for the inline row display.
    /// Strips the standard WindowsApps exe path prefix and, if still too
    /// long, inserts a middle ellipsis so both the executable name and
    /// the final args stay visible.</summary>
    public string CommandLineDisplay { get; }

    /// <summary>Process start time captured at scan time. Not mutated
    /// after construction - a process that restarts gets a fresh row.</summary>
    public DateTime StartTime { get; }

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private TimeSpan _uptime;
    [ObservableProperty] private Bitmap? _thumbnail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private bool _isClosing;

    public string UptimeDisplay => Uptime.TotalHours >= 1
        ? $"{(int)Uptime.TotalHours}h {Uptime.Minutes}m"
        : $"{Uptime.Minutes}m {Uptime.Seconds}s";

    public ExternalInstanceViewModel(
        ExternalProcessInfo info,
        Func<ExternalInstanceViewModel, Task>? onClose = null,
        Action<ExternalInstanceViewModel>? onShowPreview = null)
    {
        Pid = info.ProcessId;
        CommandLine = info.CommandLine;
        CommandLineDisplay = BuildDisplay(info.CommandLine);
        StartTime = info.StartTime;
        _onClose = onClose;
        _onShowPreview = onShowPreview;
    }

    /// <summary>
    /// Refreshes the live resource fields from a matching snapshot. Called
    /// by the parent list VM once per poll tick after it has correlated
    /// this row's PID with a <see cref="InstanceResourceSnapshot"/>.
    /// </summary>
    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        Uptime = snapshot.Uptime;
        OnPropertyChanged(nameof(UptimeDisplay));
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private async Task Close()
    {
        if (_onClose is null) return;
        IsClosing = true;
        try
        {
            await _onClose(this);
        }
        finally
        {
            IsClosing = false;
        }
    }

    private bool CanClose() => !IsClosing;

    /// <summary>
    /// Opens the lightbox-style preview window for this external
    /// instance's current <see cref="Thumbnail"/>. Mirrors the
    /// corresponding command on <see cref="ClaudeInstanceViewModel"/>.
    /// </summary>
    [RelayCommand]
    private void ShowPreview()
    {
        _onShowPreview?.Invoke(this);
    }

    /// <summary>
    /// Replaces <see cref="Thumbnail"/> with a new bitmap decoded from
    /// the supplied PNG bytes. A null or empty array is a no-op, not
    /// a clear - matches the frozen-thumbnail contract defined on
    /// <see cref="IThumbnailableViewModel"/>. Previous bitmap is
    /// disposed when replaced so we don't leak GDI handles over long
    /// launcher uptimes.
    /// </summary>
    public void UpdateThumbnailFromBytes(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return;
        var old = Thumbnail;
        using var ms = new MemoryStream(pngBytes);
        Thumbnail = new Bitmap(ms);
        old?.Dispose();
    }

    /// <summary>
    /// Explicitly blanks the thumbnail and disposes its buffer. Called
    /// when the user toggles the "Show thumbnails" setting off.
    /// </summary>
    public void ClearThumbnail()
    {
        var old = Thumbnail;
        Thumbnail = null;
        old?.Dispose();
    }

    /// <summary>
    /// Builds the inline display string. Order of operations:
    /// 1. Normalise away the common Claude exe path so rows aren't dominated
    ///    by <c>C:\Program Files\WindowsApps\Claude_1.3109.0.0_x64__.../claude.exe</c>.
    /// 2. If the normalised result is still over the limit, apply middle-
    ///    ellipsis truncation so the executable name (start) and the most
    ///    recently added flags (end) both remain visible.
    /// </summary>
    private static string BuildDisplay(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine))
            return "(command line unavailable)";

        var normalised = TryStripClaudePath(commandLine.Trim());

        if (normalised.Length <= CommandLineDisplayMaxLength)
            return normalised;

        var keep = (CommandLineDisplayMaxLength - 3) / 2;
        return normalised[..keep] + "..." + normalised[^keep..];
    }

    /// <summary>
    /// Replaces a leading quoted-or-unquoted absolute path to
    /// <c>claude.exe</c> with the bare file name, preserving any args
    /// that follow. Leaves the command line untouched if the
    /// <c>claude.exe</c> token is not found (e.g. WMI returned empty or
    /// a locale-renamed executable).
    /// </summary>
    private static string TryStripClaudePath(string commandLine)
    {
        const string exeToken = "claude.exe";
        var idx = commandLine.IndexOf(exeToken, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return commandLine;

        var startsQuoted = commandLine.StartsWith('"');
        var afterExe = commandLine[(idx + exeToken.Length)..];
        if (startsQuoted && afterExe.StartsWith('"'))
            afterExe = afterExe[1..];

        return (exeToken + afterExe).TrimEnd();
    }
}