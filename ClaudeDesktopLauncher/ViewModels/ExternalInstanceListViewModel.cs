using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Owns the list of externally-launched Claude instances - Claude
/// processes running on the system that the launcher did NOT spawn
/// (default-profile <c>claude.exe</c> from the Start menu, typically).
///
/// Refreshed on the same poll-timer tick as <see cref="IResourceMonitor"/>
/// so both the slot list and the external list update atomically from the
/// caller's perspective.
///
/// Close flow is owned here rather than in <see
/// cref="ExternalInstanceViewModel"/> so the per-row VM stays dependency-
/// free and easy to test. The confirmation dialog is destructive-severity
/// with explicit "Close Claude" / "Keep open" button text, deliberately
/// avoiding the word "Confirm" which under-sells the action.
/// </summary>
public partial class ExternalInstanceListViewModel : ObservableObject
{
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;
    private readonly IConfirmDialogService _confirmDialog;
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;

    /// <summary>Bindable collection of per-row VMs. Mutated in-place
    /// during <see cref="Refresh"/> so existing WPF/Avalonia bindings
    /// are preserved and the UI doesn't flicker.</summary>
    public ObservableCollection<ExternalInstanceViewModel> Items { get; } = new();

    [ObservableProperty] private bool _hasExternalInstances;
    [ObservableProperty] private double _totalRamMb;
    [ObservableProperty] private double _totalCpuPercent;

    public Action<ExternalInstanceViewModel>? OnShowPreview { get; set; }

    public ExternalInstanceListViewModel(
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier,
        IConfirmDialogService confirmDialog,
        IProcessService processService,
        ILoggingService logger)
    {
        _scanner = scanner;
        _classifier = classifier;
        _confirmDialog = confirmDialog;
        _processService = processService;
        _logger = logger;
    }

    /// <summary>
    /// Rescans running Claude processes, reconciles the <see cref="Items"/>
    /// collection with the current external set (add/update/remove in
    /// place), and recomputes the external-only RAM/CPU totals.
    ///
    /// <paramref name="resourceSnapshots"/> MUST contain entries for all
    /// windowed claude.exe processes - the same snapshots passed to
    /// <see cref="IResourceMonitor.GetSnapshots"/>. Used to correlate
    /// this list's PIDs with their RAM/CPU/uptime numbers without a
    /// second process-table walk.
    ///
    /// Swallows scan exceptions and logs a warning rather than
    /// propagating: a transient WMI hiccup should not take down the
    /// whole poll tick. The previous state is preserved in that case.
    /// </summary>
    public void Refresh(IReadOnlyList<InstanceResourceSnapshot> resourceSnapshots)
    {
        try
        {
            var externals = _scanner.Scan()
                .Select(p => _classifier.TryClassifyAsExternal(p))
                .Where(e => e is not null)
                .Cast<ExternalProcessInfo>()
                .ToList();

            ReconcileItems(externals, resourceSnapshots);
            RecomputeTotals();
            HasExternalInstances = Items.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"External instance scan failed: {ex.Message}");
        }
    }

    /// <summary>
    /// In-place reconciliation: remove rows whose PIDs have exited, add
    /// rows for newly-seen PIDs, and update RAM/CPU/uptime for everyone.
    /// Preserves object identity for existing rows so bindings on
    /// per-row state (IsClosing during a close operation, for example)
    /// survive the refresh.
    /// </summary>
    private void ReconcileItems(
        IReadOnlyList<ExternalProcessInfo> externals,
        IReadOnlyList<InstanceResourceSnapshot> resourceSnapshots)
    {
        var wantedPids = externals.Select(e => e.ProcessId).ToHashSet();
        var snapshotByPid = resourceSnapshots.ToDictionary(s => s.ProcessId);

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!wantedPids.Contains(Items[i].Pid))
                Items.RemoveAt(i);
        }

        foreach (var info in externals)
        {
            var row = Items.FirstOrDefault(vm => vm.Pid == info.ProcessId);
            if (row is null)
            {
                row = new ExternalInstanceViewModel(
                    info,
                    CloseAsync,
                    vm => OnShowPreview?.Invoke(vm));
                Items.Add(row);
            }

            if (snapshotByPid.TryGetValue(info.ProcessId, out var snap))
                row.UpdateFrom(snap);
        }
    }

    private void RecomputeTotals()
    {
        TotalRamMb = Math.Round(Items.Sum(i => i.RamMb), 1);
        TotalCpuPercent = Math.Round(Items.Sum(i => i.CpuPercent), 1);
    }

    /// <summary>
    /// Close callback supplied to each <see cref="ExternalInstanceViewModel"/>.
    /// Pops the destructive confirm dialog and, if the user agrees, calls
    /// <see cref="IProcessService.KillProcess"/>.
    ///
    /// Any exception from the kill path (access-denied, process already
    /// gone) is logged but does NOT propagate - the caller's RelayCommand
    /// would otherwise surface it as an unhandled async-void exception.
    /// </summary>
    private async Task CloseAsync(ExternalInstanceViewModel instance)
    {
        var options = new ConfirmDialogOptions(
            Title: "Close external Claude?",
            Message: BuildConfirmMessage(instance),
            ConfirmText: "Close Claude",
            CancelText: "Keep open",
            Severity: DialogSeverity.Destructive);

        bool confirmed;
        try
        {
            confirmed = await _confirmDialog.ConfirmAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Confirm dialog threw while asking about PID {instance.Pid}", ex);
            return;
        }

        if (!confirmed)
        {
            _logger.LogInfo($"User cancelled close of external Claude PID {instance.Pid}");
            return;
        }

        try
        {
            _processService.KillProcess(instance.Pid);
            _logger.LogInfo($"Closed external Claude PID {instance.Pid}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to close external Claude PID {instance.Pid}", ex);
        }
    }

    private static string BuildConfirmMessage(ExternalInstanceViewModel i) =>
        "This Claude window was not started by the launcher.\n" +
        "Closing it may discard unsaved conversation state.\n\n" +
        $"PID: {i.Pid}\n" +
        $"Uptime: {i.UptimeDisplay}\n" +
        $"Command: {i.CommandLineDisplay}";
}