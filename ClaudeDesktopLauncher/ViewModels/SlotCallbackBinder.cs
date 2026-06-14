using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Stateless helper that wires the per-row callbacks on
/// <see cref="SlotInstanceListViewModel"/> back to the launcher/settings/
/// refresh orchestration in <see cref="MainWindowViewModel"/>. The slot
/// VM cannot do these itself because it doesn't own
/// <see cref="AppSettings"/>, <see cref="IClaudeInstanceLauncher"/>, or
/// the refresh mechanism - those are the main window's concerns. This
/// one-shot binding concentrates the adapter layer in a single method
/// so the calling constructor stays clean.
/// </summary>
public static class SlotCallbackBinder
{
    /// <summary>
    /// Wires every per-row callback on <paramref name="slots"/> to the
    /// supplied dependencies. Call once during MainWindowViewModel
    /// construction; the slot VM will invoke these as the user
    /// interacts with individual rows (edits a nickname, clicks the
    /// kill button).
    /// </summary>
    public static void Bind(
        SlotInstanceListViewModel slots,
        AppSettings settings,
        IClaudeInstanceLauncher launcher,
        IWindowHider windowHider,
        IWindowShower windowShower,
        IThumbnailPreviewService previewService,
        Action saveSettings,
        Action refreshResources)
    {
        slots.GetSlotName = num => settings.GetSlotName(num);
        slots.OnSlotNameChanged = (slotNumber, name) =>
        {
            settings.SlotNames[slotNumber] = name;
            saveSettings();
        };
        slots.OnKillInstance = processId =>
        {
            launcher.KillInstance(processId);
            refreshResources();
        };
        // v1.10.5: Hide action just forwards to the window hider.
        // No refreshResources() here - the hidden slot stays alive,
        // the next scanner poll (on its own schedule) will move it
        // into the TrayCard list without us prodding anything.
        slots.OnHideInstance = processId => windowHider.TryHide(processId);
        // v1.10.6: Show action enumerates hidden top-level windows by PID
        // and calls ShowWindow(SW_SHOW) + SetForegroundWindow. Same
        // no-refresh rationale as Hide - the scanner poll handles the
        // TrayCard -> SlotCard transition on its own schedule.
        slots.OnShowInstance = processId => windowShower.TryShow(processId);
        slots.OnShowPreview = vm =>
            previewService.Show(vm.ProcessId, vm.Thumbnail, $"Slot {vm.InstanceNumber} - {vm.SlotName}");
    }

    /// <summary>
    /// Wires the single preview callback on
    /// <see cref="ExternalInstanceListViewModel"/>. Separate method
    /// from <see cref="Bind"/> because the external list VM owns its
    /// own service wiring (close confirm dialog, kill process) via its
    /// constructor - only the preview callback needs to be injected
    /// from the outside.
    /// </summary>
    public static void BindExternal(
        ExternalInstanceListViewModel externals,
        IThumbnailPreviewService previewService)
    {
        externals.OnShowPreview = vm =>
            previewService.Show(vm.Pid, vm.Thumbnail, $"External PID {vm.Pid}");
    }
}
