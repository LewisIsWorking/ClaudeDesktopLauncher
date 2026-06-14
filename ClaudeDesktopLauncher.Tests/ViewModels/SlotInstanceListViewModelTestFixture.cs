using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Shared mock graph and helpers for <c>SlotInstanceListViewModel</c>
/// tests. Extracted so individual test files stay under the 200-line
/// limit without duplicating wiring.
///
/// <para>
/// Each test class news-up its own instance of this fixture, giving
/// fresh substitutes per test run. Classifier/scanner interactions are
/// driven through <see cref="ReturnSlots"/> which keeps the arrange
/// phase to one line per scenario.
/// </para>
/// </summary>
public class SlotInstanceListViewModelTestFixture
{
    public IClaudeProcessScanner Scanner { get; } = Substitute.For<IClaudeProcessScanner>();
    public IClaudeProcessClassifier Classifier { get; } = Substitute.For<IClaudeProcessClassifier>();
    public ISlotInitialiser SlotInitialiser { get; } = Substitute.For<ISlotInitialiser>();
    public ILoggingService Logger { get; } = Substitute.For<ILoggingService>();

    public SlotInstanceListViewModel CreateSut() =>
        new(Scanner, Classifier, SlotInitialiser, Logger);

    public static ClaudeProcessInfo Claude(int pid) =>
        new(pid, "", DateTime.UtcNow);

    public static InstanceResourceSnapshot Snap(
        int pid,
        int instanceNumber = 0,
        double cpu = 0,
        double ramMb = 0,
        bool isTrayResident = false) =>
        new(pid, instanceNumber, cpu, (long)(ramMb * 1024 * 1024), TimeSpan.Zero, isTrayResident);

    /// <summary>
    /// Configures the scanner to return the given claude procs and the
    /// classifier to map each one to a slot (or <c>null</c> for not-slot)
    /// based on the supplied mappings. <paramref name="mappings"/> uses
    /// <c>(pid, slotNumber)</c> tuples - pass a null <c>slotNumber</c> to
    /// make the classifier treat that process as non-slot. All slots are
    /// classified as windowed (not tray-resident). Use
    /// <see cref="ReturnSlotsWithTray"/> for tray-resident scenarios.
    /// </summary>
    public void ReturnSlots(params (int pid, int? slotNumber)[] mappings)
    {
        var procs = mappings.Select(m => Claude(m.pid)).ToArray();
        Scanner.Scan().Returns(procs);
        for (var i = 0; i < mappings.Length; i++)
        {
            var (pid, slot) = mappings[i];
            Classifier.TryClassifyAsSlot(procs[i])
                .Returns(slot.HasValue ? new SlotProcessInfo(pid, slot.Value) : null);
        }
    }

    /// <summary>
    /// Like <see cref="ReturnSlots"/> but tags each slot with a
    /// tray-resident flag. The mapping tuple is
    /// <c>(pid, slotNumber, isTrayResident)</c>.
    /// </summary>
    public void ReturnSlotsWithTray(params (int pid, int slotNumber, bool isTrayResident)[] mappings)
    {
        var procs = mappings.Select(m => Claude(m.pid)).ToArray();
        Scanner.Scan().Returns(procs);
        for (var i = 0; i < mappings.Length; i++)
        {
            var (pid, slot, tray) = mappings[i];
            Classifier.TryClassifyAsSlot(procs[i])
                .Returns(new SlotProcessInfo(pid, slot, tray));
        }
    }
}
