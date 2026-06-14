using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Classifies a <see cref="ClaudeProcessInfo"/> as either a launcher-managed
/// slot process or an externally-launched process, based on whether its
/// command line contains the <c>--user-data-dir=...\ClaudeSlotN</c> flag.
///
/// A single process is exactly one of the two classifications. Both
/// <c>TryClassifyAs*</c> methods return <see langword="null"/> when the
/// input does not match, so callers pick the method that matches the
/// category they care about.
/// </summary>
public interface IClaudeProcessClassifier
{
    SlotProcessInfo? TryClassifyAsSlot(ClaudeProcessInfo process);
    ExternalProcessInfo? TryClassifyAsExternal(ClaudeProcessInfo process);
}