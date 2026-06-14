using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Wires <see cref="ISlotProcessMonitor.SlotClosed"/> to
/// <see cref="ISlotSeedCache.TrySnapshot"/>, with a short delay so Electron
/// helper processes finish releasing file locks on the slot's data directory
/// before we attempt to copy from it.
///
/// Kept deliberately tiny: this is the event-driven bridge between the
/// monitor and the cache. The delay is implemented via Task.Delay rather
/// than a Thread.Sleep so the timer thread can return promptly.
/// </summary>
public class SlotSeedCacheUpdater : IDisposable
{
    /// <summary>
    /// Wait a few seconds after a slot window closes before snapshotting, so
    /// the Electron helper processes have time to exit and release their
    /// exclusive file locks.
    /// </summary>
    public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromSeconds(5);

    private readonly ISlotProcessMonitor _monitor;
    private readonly ISlotSeedCache _cache;
    private readonly ILoggingService _logger;
    private readonly TimeSpan _settleDelay;
    private bool _subscribed;

    public SlotSeedCacheUpdater(
        ISlotProcessMonitor monitor,
        ISlotSeedCache cache,
        ILoggingService logger)
        : this(monitor, cache, logger, DefaultSettleDelay) { }

    public SlotSeedCacheUpdater(
        ISlotProcessMonitor monitor,
        ISlotSeedCache cache,
        ILoggingService logger,
        TimeSpan settleDelay)
    {
        _monitor = monitor;
        _cache = cache;
        _logger = logger;
        _settleDelay = settleDelay;
    }

    public void Start()
    {
        if (_subscribed) return;
        _monitor.SlotClosed += OnSlotClosed;
        _subscribed = true;
        _logger.LogInfo("Seed cache updater subscribed to slot-closed events");
    }

    public void Stop()
    {
        if (!_subscribed) return;
        _monitor.SlotClosed -= OnSlotClosed;
        _subscribed = false;
        _logger.LogInfo("Seed cache updater unsubscribed");
    }

    public void Dispose() => Stop();

    private void OnSlotClosed(object? sender, SlotClosedEventArgs args)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_settleDelay).ConfigureAwait(false);
                _logger.LogInfo(
                    $"Attempting seed-cache snapshot from just-closed slot {args.Slot.SlotNumber}");
                _cache.TrySnapshot(args.Slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Seed-cache snapshot attempt for slot {args.Slot.SlotNumber} failed",
                    ex);
            }
        });
    }
}