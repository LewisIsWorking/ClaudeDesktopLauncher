using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ComeOnOverDesktopLauncher.Core.Services;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Core.Services.Linux;
using ComeOnOverDesktopLauncher.Services;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Linux;
using ComeOnOverDesktopLauncher.ViewModels;
using ComeOnOverDesktopLauncher.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ComeOnOverDesktopLauncher;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private ITrayIconService? _trayIconService;
    private ISlotProcessMonitor? _slotProcessMonitor;
    private SlotSeedCacheUpdater? _seedCacheUpdater;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = ConfigureServices().BuildServiceProvider();
        HookGlobalExceptionHandlers();
        _serviceProvider.GetRequiredService<IClaudePathCache>().Refresh();

        // Self-heal any Start Menu shortcut the Velopack updater may
        // have left empty during a previous update apply. On Linux
        // this resolves to NoOpShortcutHealer which returns
        // SkippedDevBuild without touching the filesystem. See
        // docs/dev/VELOPACK.md for the Windows upstream bug.
        _serviceProvider.GetRequiredService<IShortcutHealer>().HealIfMissing();

        _slotProcessMonitor = _serviceProvider.GetRequiredService<ISlotProcessMonitor>();
        _slotProcessMonitor.Start(TimeSpan.FromSeconds(2));
        _seedCacheUpdater = _serviceProvider.GetRequiredService<SlotSeedCacheUpdater>();
        _seedCacheUpdater.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startMinimised = desktop.Args?.Contains("--minimised") == true;
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            _mainWindow = new MainWindow
            {
                DataContext = viewModel,
                SnapshotService = _serviceProvider.GetRequiredService<IWindowSnapshotService>()
            };

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SetupTrayIcon(viewModel, desktop);

            if (!startMinimised)
                _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(MainWindowViewModel viewModel, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIconService = _serviceProvider!.GetRequiredService<ITrayIconService>();
        _trayIconService.Initialise(
            onShow: ShowMainWindow,
            onLaunchClaude: () => viewModel.LaunchInstancesCommand.Execute(null),
            onQuit: () =>
            {
                _seedCacheUpdater?.Stop();
                _slotProcessMonitor?.Stop();
                _trayIconService.Dispose();
                desktop.Shutdown();
            });
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>Catches unhandled UI-thread exceptions thrown during
    /// dispatcher operations (Invoke/Post). Does NOT catch exceptions
    /// thrown synchronously inside a Win32 WndProc - see
    /// docs/dev/LEARNINGS.md.</summary>
    private void HookGlobalExceptionHandlers()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            var logger = _serviceProvider?.GetService<ILoggingService>();
            logger?.LogError(
                $"Swallowed UI exception: {e.Exception.GetType().FullName}: {e.Exception.Message}",
                e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var logger = _serviceProvider?.GetService<ILoggingService>();
            var ex = e.ExceptionObject as Exception;
            logger?.LogError(
                $"Unhandled AppDomain exception (terminating={e.IsTerminating}): {ex?.GetType().FullName}: {ex?.Message}",
                ex);
        };
    }

    /// <summary>Builds the DI container with platform-specific
    /// service variants chosen via OperatingSystem.IsWindows. Linux
    /// variants live in *.Services.Linux and are mostly no-op stubs
    /// for the v1.10.19 build-and-run MVP.</summary>
    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Cross-platform services -----------------------------------------
        services.AddSingleton<IFileSystem, WindowsFileSystem>();
        services.AddSingleton<ILoggingService, FileLoggingService>();
        services.AddSingleton<IProcessService, SystemProcessService>();
        services.AddSingleton<IClaudeProcessClassifier, RegexClaudeProcessClassifier>();
        services.AddSingleton<IThumbnailPreviewService, AvaloniaThumbnailPreviewService>();
        services.AddSingleton<IClaudePathCache, ClaudePathCache>();
        services.AddSingleton<IClaudeVersionResolver, ClaudeVersionResolver>();
        services.AddSingleton<IClaudeInstanceLauncher, ClaudeInstanceLauncher>();
        services.AddSingleton<ISlotManager, SlotManager>();
        services.AddSingleton<ISlotSeedCache, FileSlotSeedCache>();
        services.AddSingleton<ISlotProcessMonitor, SlotProcessMonitor>();
        services.AddSingleton<SlotSeedCacheUpdater>();
        services.AddSingleton<ISlotInitialiser, SlotInitialiser>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton(provider => provider.GetRequiredService<ISettingsService>().Load());
        services.AddSingleton<IResourceMonitor, ResourceMonitor>();
        services.AddSingleton<IClaudeDiskUsageService, ClaudeDiskUsageService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IVersionProvider, VersionProvider>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IWindowSnapshotService, WindowSnapshotService>();
        services.AddSingleton<IConfirmDialogService, ConfirmDialogService>();
        services.AddTransient<SlotInstanceListViewModel>();
        services.AddTransient<ExternalInstanceListViewModel>();
        services.AddTransient<MainWindowViewModel>();

        // Platform-specific services --------------------------------------
#if WINDOWS
        if (OperatingSystem.IsWindows())
            RegisterWindowsServices(services);
        else
#endif
            RegisterLinuxServices(services);

        return services;
    }

#if WINDOWS
#pragma warning disable CA1416 // Windows-only types guarded by OperatingSystem.IsWindows()
    private static void RegisterWindowsServices(ServiceCollection services)
    {
        services.AddSingleton<IClaudeProcessScanner, WmiClaudeProcessScanner>();
        services.AddSingleton<IWindowThumbnailService, PrintWindowThumbnailService>();
        services.AddSingleton<IRegistryService, WindowsRegistryService>();
        services.AddSingleton<IClaudePathResolver, ClaudePathResolver>();
        services.AddSingleton<IAutoUpdateService, VelopackAutoUpdateService>();
        services.AddSingleton<IUpdateApplyFailureDetector, VelopackLogApplyFailureDetector>();
        services.AddSingleton<IWindowHider, Win32WindowHider>();
        services.AddSingleton<IWindowShower, Win32WindowShower>();
        services.AddSingleton<IShellLinkWriter, WScriptShellLinkWriter>();
        services.AddSingleton<IIconCacheRefresher, WindowsIconCacheRefresher>();
        services.AddSingleton<IShortcutHealer, WindowsShortcutHealer>();
    }
#pragma warning restore CA1416
#endif

    private static void RegisterLinuxServices(ServiceCollection services)
    {
        services.AddSingleton<IClaudeProcessScanner, ProcfsClaudeProcessScanner>();
        services.AddSingleton<IWindowThumbnailService, NoOpThumbnailService>();
        services.AddSingleton<IRegistryService, NoOpRegistryService>();
        services.AddSingleton<IClaudePathResolver, LinuxClaudePathResolver>();
        services.AddSingleton<IAutoUpdateService, NoOpAutoUpdateService>();
        services.AddSingleton<IUpdateApplyFailureDetector, NoOpUpdateApplyFailureDetector>();
        services.AddSingleton<IWindowHider, NoOpWindowHider>();
        services.AddSingleton<IWindowShower, NoOpWindowShower>();
        services.AddSingleton<IShellLinkWriter, NoOpShellLinkWriter>();
        services.AddSingleton<IIconCacheRefresher, NoOpIconCacheRefresher>();
        services.AddSingleton<IShortcutHealer, NoOpShortcutHealer>();
    }
}
