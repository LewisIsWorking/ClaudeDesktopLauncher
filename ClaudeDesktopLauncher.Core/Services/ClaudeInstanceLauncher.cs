using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Launches and terminates Claude Desktop instances.
/// Uses unique --user-data-dir per slot so login sessions persist between launches.
/// Every launch attempt is logged with the resolved path, data dir and outcome
/// so that silent failures can be diagnosed from the log file.
///
/// Owns the full launch sequence (slot picking + seeding + process
/// start) so view-model and future CLI callers only need to say "launch
/// N instances" without re-implementing the coordination dance.
/// </summary>
public class ClaudeInstanceLauncher : IClaudeInstanceLauncher
{
    private readonly IClaudePathResolver _pathResolver;
    private readonly ISlotManager _slotManager;
    private readonly ISlotInitialiser _slotInitialiser;
    private readonly IProcessService _processService;
    private readonly ILoggingService _logger;

    public ClaudeInstanceLauncher(
        IClaudePathResolver pathResolver,
        ISlotManager slotManager,
        ISlotInitialiser slotInitialiser,
        IProcessService processService,
        ILoggingService logger)
    {
        _pathResolver = pathResolver;
        _slotManager = slotManager;
        _slotInitialiser = slotInitialiser;
        _processService = processService;
        _logger = logger;
    }

    public IReadOnlyList<LaunchSlot> LaunchInstances(int count)
    {
        var slots = _slotManager.GetNextFreeSlots(count);
        _logger.LogInfo(
            $"Picked free slot(s): {string.Join(", ", slots.Select(s => s.SlotNumber))}");
        foreach (var slot in slots)
        {
            _slotInitialiser.EnsureInitialised(slot);
            LaunchSlot(slot);
        }
        return slots;
    }

    public void LaunchSlot(LaunchSlot slot)
    {
        _logger.LogInfo($"LaunchSlot called for slot {slot.SlotNumber}");

        var exePath = _pathResolver.ResolveClaudeExePath();
        if (exePath is null)
        {
            _logger.LogError("Claude executable not found - aborting launch");
            throw new InvalidOperationException("Claude executable not found. Is Claude Desktop installed?");
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            slot.DataDirectoryName);
        var args = $"--user-data-dir=\"{dataDir}\"";

        _logger.LogInfo($"Starting process: {exePath} {args}");
        try
        {
            // Log callback captures slot.SlotNumber (always available)
            // rather than pid (would be a race with the background
            // reader thread that BeginErrorReadLine spawns). PID lands
            // in the "completed" log line below so correlation is
            // still possible via slot+time.
            var pid = _processService.StartWithStderrPipe(
                exePath, args,
                line => _logger.LogWarning($"[claude slot {slot.SlotNumber} stderr] {line}"));
            _logger.LogInfo($"Process.Start completed for slot {slot.SlotNumber} (pid {pid})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Process.Start threw for slot {slot.SlotNumber}", ex);
            throw;
        }
    }

    public void KillInstance(int processId)
    {
        _logger.LogInfo($"Killing process {processId}");
        _processService.KillProcess(processId);
    }

    /// <summary>
    /// Returns the number of Claude instances with a visible window.
    /// Uses windowed count to avoid counting Electron background child processes.
    /// </summary>
    public int GetRunningInstanceCount() =>
        _processService.CountByNameWithWindow("claude");
}
