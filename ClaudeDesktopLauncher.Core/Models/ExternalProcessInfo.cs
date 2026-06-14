namespace ClaudeDesktopLauncher.Core.Models;

/// <summary>
/// A running claude.exe process whose command line does <b>not</b> contain
/// the launcher's <c>--user-data-dir=...\ClaudeSlotN</c> flag.
/// Produced by <see cref="Services.Interfaces.IClaudeProcessClassifier"/>
/// and surfaced in the "External Claude instances" section of the launcher UI.
///
/// Kept structurally distinct from <see cref="SlotProcessInfo"/> so that
/// callers cannot accidentally conflate the two at a type level.
/// </summary>
public record ExternalProcessInfo(int ProcessId, string CommandLine, DateTime StartTime);