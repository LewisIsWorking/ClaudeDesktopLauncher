using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Pure state machine that, given a stream of
/// <see cref="IClaudeProcessScanner.Scan"/> snapshots, emits a
/// <see cref="LaunchSlot"/> for each slot that was seen in a previous tick
/// but is absent from the current tick.
///
/// Separated from <see cref="SlotProcessMonitor"/> so that unit tests can
/// drive the tick logic directly without needing a real timer.
/// </summary>
public class SlotProcessTickRunner
{
    private readonly IClaudeProcessScanner _scanner;
    private readonly IClaudeProcessClassifier _classifier;
    private HashSet<int> _previouslySeenSlots = new();

    public SlotProcessTickRunner(
        IClaudeProcessScanner scanner,
        IClaudeProcessClassifier classifier)
    {
        _scanner = scanner;
        _classifier = classifier;
    }

    public IReadOnlyList<LaunchSlot> Tick()
    {
        var currentSlots = _scanner.Scan()
            .Select(p => _classifier.TryClassifyAsSlot(p))
            .Where(info => info is not null)
            .Select(info => info!.SlotNumber)
            .ToHashSet();

        var closed = _previouslySeenSlots
            .Where(n => !currentSlots.Contains(n))
            .Select(n => new LaunchSlot(n))
            .ToList();

        _previouslySeenSlots = currentSlots;
        return closed;
    }
}