using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Creates and manages the system tray icon with a right-click context menu.
/// </summary>
public class TrayIconService : ITrayIconService
{
    private TrayIcon? _trayIcon;

    public void Initialise(Action onShow, Action onLaunchClaude, Action onQuit)
    {
        // Uses the multi-resolution appicon.ico (16/24/32/48/64/128/256
        // nested) so Windows picks the appropriate size for the tray -
        // typically 16 or 20 depending on DPI scaling.
        var icon = new WindowIcon(
            AssetLoader.Open(new Uri("avares://ClaudeDesktopLauncher/Assets/appicon.ico")));

        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Show");
        showItem.Click += (_, _) => onShow();
        menu.Add(showItem);

        menu.Add(new NativeMenuItemSeparator());

        var launchItem = new NativeMenuItem("Launch Claude");
        launchItem.Click += (_, _) => onLaunchClaude();
        menu.Add(launchItem);

        menu.Add(new NativeMenuItemSeparator());

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => onQuit();
        menu.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Claude Desktop Launcher",
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) => onShow();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
