using System;
using System.Runtime.Versioning;
using Avalonia;
using ClaudeDesktopLauncher.Services;
using Velopack;

namespace ClaudeDesktopLauncher;

/// <summary>
/// Entry point. Marked Windows-only because the app uses Windows-specific APIs
/// (registry for startup, MSIX package queries for Claude path resolution).
/// When cross-platform support is added in v2.0, platform-specific implementations
/// behind IRegistryService and IClaudePathResolver will remove this requirement.
///
/// <para>
/// v1.10.0: <see cref="VelopackApp.Build"/> must be invoked as the very
/// first operation in <see cref="Main"/>, before Avalonia boots. Velopack
/// uses the install/update/uninstall hooks as separate process invocations:
/// <c>Update.exe</c> launches our app with special command-line arguments
/// when it needs us to handle a lifecycle event, and <c>VelopackApp.Run()</c>
/// intercepts those invocations and exits before any Avalonia initialisation
/// would fire a window. Putting it later than this point means the hook
/// UX stutters (brief window flash) or outright breaks (hook never fires).
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnFirstRun(_ => VelopackAutoUpdateService.FirstRunAfterUpdate = true)
            .Run();

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
