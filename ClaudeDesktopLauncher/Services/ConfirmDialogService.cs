using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.Views;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Default <see cref="IConfirmDialogService"/> implementation. Locates the
/// active <see cref="Avalonia.Controls.Window"/> via the current classic
/// desktop application lifetime and opens a <see cref="ConfirmDialog"/>
/// modally against it.
///
/// Fast path: when the caller is already on the UI thread (the common
/// case - tick-timer-driven VMs and button-click handlers), opens the
/// dialog directly and awaits its result. When called from a background
/// thread, marshals to the UI thread first via
/// <see cref="Dispatcher.UIThread"/> before opening the dialog.
///
/// Returns <c>false</c> rather than throwing when no owner window is
/// available (application is shutting down, or called before the main
/// window is up): callers then naturally degrade to "user didn't confirm"
/// semantics instead of bubbling an exception through an async click
/// handler. A warning is logged when this happens so the situation is
/// visible in the log rather than silently dropped.
/// </summary>
public class ConfirmDialogService : IConfirmDialogService
{
    private readonly ILoggingService _logger;

    public ConfirmDialogService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConfirmAsync(ConfirmDialogOptions options)
    {
        _logger.LogInfo($"ConfirmAsync entered: title='{options.Title}', uiThread={Dispatcher.UIThread.CheckAccess()}");
        var owner = TryGetOwnerWindow();
        if (owner is null)
        {
            _logger.LogWarning(
                "Cannot show confirm dialog - no active main window found. " +
                "Treating as 'not confirmed'.");
            return false;
        }
        _logger.LogInfo($"Owner window resolved: '{owner.Title}'");

        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(() => ConfirmAsync(options));

        return await ShowDialogAsync(owner, options);
    }

    /// <summary>
    /// UI-thread dialog orchestration: construct, apply options, show
    /// modally, return the result. Each step is logged so if the flow
    /// ever hangs again, the log pinpoints the step that never
    /// completed. Logging is permanent - never stripped - because this
    /// exact hang reproduced silently during v1.8.0 development and the
    /// log trace is what localised it.
    /// </summary>
    private async Task<bool> ShowDialogAsync(
        Avalonia.Controls.Window owner,
        ConfirmDialogOptions options)
    {
        try
        {
            _logger.LogInfo("Constructing ConfirmDialog");
            var dialog = new ConfirmDialog();
            _logger.LogInfo("Applying options to dialog");
            dialog.Apply(options);
            _logger.LogInfo("Awaiting ShowDialog on UI thread");
            await dialog.ShowDialog(owner);
            _logger.LogInfo($"Dialog closed, result={dialog.Result}");
            return dialog.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError("ConfirmDialog orchestration threw", ex);
            throw;
        }
    }

    /// <summary>
    /// Pulls the main window off the current application's lifetime.
    /// Returns <see langword="null"/> when:
    /// <list type="bullet">
    ///   <item><see cref="Application.Current"/> is null - happens in
    ///   unit tests and during early startup.</item>
    ///   <item>The lifetime is non-classic-desktop (headless tests,
    ///   mobile targets) - we only support desktop modal dialogs.</item>
    ///   <item>The lifetime exists but
    ///   <see cref="IClassicDesktopStyleApplicationLifetime.MainWindow"/>
    ///   is null - late-startup window not yet assigned or shutdown
    ///   already under way.</item>
    /// </list>
    /// </summary>
    private static Avalonia.Controls.Window? TryGetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}