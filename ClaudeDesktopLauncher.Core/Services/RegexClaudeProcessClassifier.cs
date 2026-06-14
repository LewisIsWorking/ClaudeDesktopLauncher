using System.Text.RegularExpressions;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Regex-based <see cref="IClaudeProcessClassifier"/>. A process is a
/// slot process if its command line contains
/// <c>--user-data-dir="...\ClaudeSlotN"</c> (with or without surrounding
/// quotes, case-insensitive). Everything else is external.
///
/// The regex is compiled once and shared across instances.
/// </summary>
public class RegexClaudeProcessClassifier : IClaudeProcessClassifier
{
    private static readonly Regex SlotPattern = new(
        @"--user-data-dir=""?[^""]*[\\/]ClaudeSlot(\d+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SlotProcessInfo? TryClassifyAsSlot(ClaudeProcessInfo process)
    {
        if (string.IsNullOrEmpty(process.CommandLine)) return null;

        var match = SlotPattern.Match(process.CommandLine);
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups[1].Value, out var slotNumber)) return null;

        return new SlotProcessInfo(process.ProcessId, slotNumber, IsTrayResident: !process.IsWindowed);
    }

    public ExternalProcessInfo? TryClassifyAsExternal(ClaudeProcessInfo process)
    {
        // An empty-cmdline process (access-denied under WMI) is treated as
        // external rather than hidden, so the user can at least see a PID
        // and decide whether to investigate.
        if (string.IsNullOrEmpty(process.CommandLine))
            return new ExternalProcessInfo(process.ProcessId, string.Empty, process.StartTime);

        if (SlotPattern.IsMatch(process.CommandLine)) return null;

        return new ExternalProcessInfo(process.ProcessId, process.CommandLine, process.StartTime);
    }
}