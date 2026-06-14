using System;
using Avalonia;
#if WINDOWS
using Velopack;
using ClaudeDesktopLauncher.Services;
#endif

namespace ClaudeDesktopLauncher;

/// <summary>
/// Entry point.
///
/// <para>Windows: VelopackApp.Build must run as the very first
/// operation in Main, before Avalonia boots, because Velopack uses
/// our exe for its install/update/uninstall hooks as separate
/// process invocations and the hook handler must short-circuit
/// before any Avalonia window would flash up.</para>
///
/// <para>Linux: no Velopack on this platform (auto-update is
/// deferred to a later milestone), so Main jumps straight into the
/// Avalonia bootstrap.</para>
/// </summary>
sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if WINDOWS
        VelopackApp.Build()
            .OnFirstRun(_ => VelopackAutoUpdateService.FirstRunAfterUpdate = true)
            .Run();
#endif

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
