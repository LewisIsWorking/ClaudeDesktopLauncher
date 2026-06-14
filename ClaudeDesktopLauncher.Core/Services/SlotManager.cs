using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Manages Claude Desktop instance slots.
/// Slots are fixed and named to preserve login sessions between launches.
///
/// Slot occupancy (<see cref="GetNextFreeSlots"/>) is computed by scanning
/// all running claude.exe processes via <see cref="IClaudeProcessScanner"/>,
/// classifying each one as slot-or-external via
/// <see cref="IClaudeProcessClassifier"/>, and collecting the slot numbers.
/// </summary>
public class SlotManager : ISlotManager
{
    /// <summary>
    /// Safety cap used by <see cref="GetNextFreeSlots"/> to prevent an
    /// infinite loop if slot state is somehow malformed. 100 is well beyond
    /// any realistic user case (UI caps at 10).
    /// </summary>
    private const int MaxSlotScan = 100;

    private readonly IProcessService _processService;
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;

    public SlotManager(
        IProcessService processService,
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier)
    {
        _processService = processService;
        _scanner = scanner;
        _classifier = classifier;
    }

    public IReadOnlyList<LaunchSlot> GetSlots(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

        return Enumerable.Range(1, count)
            .Select(i => new LaunchSlot(i))
            .ToList();
    }

    /// <summary>
    /// Returns the next slot based on windowed instance count.
    /// Avoids counting Electron's many background child processes.
    /// </summary>
    public LaunchSlot GetNextAvailableSlot()
    {
        var runningCount = _processService.CountByNameWithWindow("claude");
        return new LaunchSlot(runningCount + 1);
    }

    public IReadOnlyList<LaunchSlot> GetNextFreeSlots(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

        var occupied = _scanner.Scan()
            .Select(p => _classifier.TryClassifyAsSlot(p))
            .Where(info => info is not null)
            .Select(info => info!.SlotNumber)
            .ToHashSet();

        var free = new List<LaunchSlot>(count);
        for (var n = 1; n <= MaxSlotScan && free.Count < count; n++)
        {
            if (!occupied.Contains(n))
                free.Add(new LaunchSlot(n));
        }

        if (free.Count < count)
            throw new InvalidOperationException(
                $"Could not find {count} free slot(s) within the first {MaxSlotScan} slot numbers.");

        return free;
    }
}