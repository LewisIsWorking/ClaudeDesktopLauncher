using System.Timers;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Default <see cref="ISlotProcessMonitor"/>. Uses <see cref="System.Timers.Timer"/>
/// to poll and delegates transition detection to <see cref="SlotProcessTickRunner"/>.
/// </summary>
public class SlotProcessMonitor : ISlotProcessMonitor, IDisposable
{
    private readonly SlotProcessTickRunner _runner;
    private readonly ILoggingService _logger;
    private System.Timers.Timer? _timer;

    public event EventHandler<SlotClosedEventArgs>? SlotClosed;

    public SlotProcessMonitor(
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier,
        ILoggingService logger)
    {
        _runner = new SlotProcessTickRunner(scanner, classifier);
        _logger = logger;
    }

    public void Start(TimeSpan pollInterval)
    {
        Stop();
        _timer = new System.Timers.Timer(pollInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
        _logger.LogInfo($"Slot process monitor started (poll every {pollInterval.TotalSeconds}s)");
    }

    public void Stop()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
        _timer = null;
        _logger.LogInfo("Slot process monitor stopped");
    }

    public void Dispose() => Stop();

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            foreach (var closedSlot in _runner.Tick())
            {
                _logger.LogInfo($"Detected slot {closedSlot.SlotNumber} closed");
                SlotClosed?.Invoke(this, new SlotClosedEventArgs(closedSlot));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Slot process monitor tick failed", ex);
        }
    }
}