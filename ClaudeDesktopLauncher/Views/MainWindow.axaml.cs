using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClaudeDesktopLauncher.Services.Interfaces;
using ClaudeDesktopLauncher.ViewModels;
using ClaudeDesktopLauncher.Views.Controls;

namespace ClaudeDesktopLauncher.Views;

public partial class MainWindow : Window
{
    private const double BreakpointWidth = 900;

    // Persistent WebView2 profile folder — user logs in to claude.ai once
    // and auth cookies survive restarts. Set via reflection on
    // WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder so the
    // code compiles cross-platform without #if guards.
    private static readonly string WebViewProfileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeDesktopLauncher", "webview-profile");

    public IWindowSnapshotService? SnapshotService { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        var totalsRow = this.FindControl<ResourceTotalsRow>("TotalsRow");
        if (totalsRow is not null)
            totalsRow.CopyClicked += OnCopyScreenshotClick;

        WireContextMenu();
        UsageWebView.EnvironmentRequested += OnUsageEnvironmentRequested;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            ApplyLayout(Bounds.Width);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyLayout(Bounds.Width);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout(e.NewSize.Width);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is not TextBox)
            Focus();
    }

    // -------------------------------------------------------------------
    // Layout engine
    // -------------------------------------------------------------------

    private void ApplyLayout(double width)
    {
        // Preserve the left panel scroll position across layout changes.
        // ApplyLayout rebuilds the Grid definitions which resets the
        // ScrollViewer offset to (0,0) without this save/restore.
        var scrollOffset = LauncherPanel.Offset;

        var usageOnLeft = (DataContext as MainWindowViewModel)?.UsagePanelOnLeft ?? false;
        if (width < BreakpointWidth)
            ApplyVerticalLayout();
        else
            ApplyHorizontalLayout(usageOnLeft);

        LauncherPanel.Offset = scrollOffset;
    }

    private void ApplyHorizontalLayout(bool usageOnLeft)
    {
        MainGrid.ColumnDefinitions = new ColumnDefinitions("440,4,*");
        MainGrid.RowDefinitions = new RowDefinitions("*");

        var launcherCol = usageOnLeft ? 2 : 0;
        var usageCol    = usageOnLeft ? 0 : 2;

        Grid.SetRow(LauncherPanel, 0);  Grid.SetColumn(LauncherPanel, launcherCol);
        Grid.SetRow(PanelSplitter, 0);  Grid.SetColumn(PanelSplitter, 1);
        Grid.SetRow(UsageWebView, 0);   Grid.SetColumn(UsageWebView, usageCol);

        PanelSplitter.ResizeDirection = GridResizeDirection.Columns;
    }

    private void ApplyVerticalLayout()
    {
        MainGrid.ColumnDefinitions = new ColumnDefinitions("*");
        MainGrid.RowDefinitions = new RowDefinitions("*,4,320");

        Grid.SetRow(LauncherPanel, 0);  Grid.SetColumn(LauncherPanel, 0);
        Grid.SetRow(PanelSplitter, 1);  Grid.SetColumn(PanelSplitter, 0);
        Grid.SetRow(UsageWebView, 2);   Grid.SetColumn(UsageWebView, 0);

        PanelSplitter.ResizeDirection = GridResizeDirection.Rows;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UsagePanelOnLeft))
            ApplyLayout(Bounds.Width);
    }

    // -------------------------------------------------------------------
    // GridSplitter context menu (option B)
    // -------------------------------------------------------------------

    private MenuItem? _toggleMenuItem;

    private void WireContextMenu()
    {
        _toggleMenuItem = new MenuItem { Header = "" };
        _toggleMenuItem.Click += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ToggleUsagePanelPositionCommand.Execute(null);
        };

        var menu = new ContextMenu();
        menu.Items.Add(_toggleMenuItem);
        menu.Opening += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm && _toggleMenuItem is not null)
                _toggleMenuItem.Header = vm.UsagePanelPositionMenuText;
        };

        PanelSplitter.ContextMenu = menu;
    }

    // -------------------------------------------------------------------
    // WebView: auth persistence + redirect fix
    // -------------------------------------------------------------------

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
            // Non-Windows platform or future package change — skip silently.
        }
    }

    private void OnUsageNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs args)
    {
        var uri = UsageWebView.Source?.ToString()?.TrimEnd('/');
        if (uri == "https://claude.ai")
            UsageWebView.Source = new Uri("https://claude.ai/settings/usage");
    }

    // -------------------------------------------------------------------
    // Copy screenshot
    // -------------------------------------------------------------------

    private async void OnCopyScreenshotClick(object? sender, RoutedEventArgs e)
    {
        if (SnapshotService is null) return;
        var ok = await SnapshotService.CaptureAndCopyAsync(this);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.StatusMessage = ok
                ? "Screenshot copied to clipboard"
                : "Screenshot failed - check logs";
        }
    }
}
