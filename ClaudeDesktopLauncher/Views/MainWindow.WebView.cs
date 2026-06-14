using System;
using System.IO;
using Avalonia.Controls;

namespace ClaudeDesktopLauncher.Views;

/// <summary>
/// Windows-only WebView wiring for the Claude usage dashboard.
/// Compiled only when '$(OS)' == 'Windows_NT' so Linux builds
/// don't need the Avalonia.Controls.WebView package. The Linux
/// side just leaves the UsagePanelHost ContentControl empty.
///
/// <para>The WebView used to live directly in MainWindow.axaml as a
/// NativeWebView element. That made the XAML un-compilable on
/// platforms missing the WebView2 package, so v1.10.19 moved it
/// into a placeholder ContentControl plus this partial class.</para>
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Persistent WebView2 profile folder - user logs in to claude.ai
    /// once and auth cookies survive restarts. Set via reflection on
    /// WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder so
    /// the code compiles without referencing the Windows-specific
    /// args type directly.
    /// </summary>
    private static readonly string WebViewProfileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeDesktopLauncher", "webview-profile");

    private NativeWebView? _usageWebView;

    /// <summary>
    /// Builds the NativeWebView and drops it into UsagePanelHost.
    /// Called from the cross-platform constructor via a partial
    /// method - on Linux this implementation isn't compiled and
    /// the partial method dispatches to nothing.
    /// </summary>
    partial void InitializeUsagePanel()
    {
        _usageWebView = new NativeWebView
        {
            Source = new Uri("https://claude.ai/settings/usage"),
            // Focusable=False prevents the v1.10.18 crash where
            // CoreWebView2Controller.MoveFocus throws ArgumentException
            // for transient WebView2 states during window activation.
            // Mouse interaction still works (pointer events bypass focus).
            Focusable = false
        };
        _usageWebView.EnvironmentRequested += OnUsageEnvironmentRequested;
        _usageWebView.NavigationCompleted += OnUsageNavigationCompleted;

        UsagePanelHost.Content = _usageWebView;
    }

    private static void OnUsageEnvironmentRequested(
        object? sender,
        WebViewEnvironmentRequestedEventArgs args)
    {
        try
        {
            args.GetType().GetProperty("UserDataFolder")
                ?.SetValue(args, WebViewProfileFolder);
        }
        catch
        {
            // Future package change - skip silently.
        }
    }

    private void OnUsageNavigationCompleted(
        object? sender,
        WebViewNavigationCompletedEventArgs args)
    {
        if (_usageWebView is null) return;
        var uri = _usageWebView.Source?.ToString()?.TrimEnd('/');
        if (uri == "https://claude.ai")
            _usageWebView.Source = new Uri("https://claude.ai/settings/usage");
    }
}
