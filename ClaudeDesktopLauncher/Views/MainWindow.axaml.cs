using System;
using System.ComponentModel;
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

    public IWindowSnapshotService? SnapshotService { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        var totalsRow = this.FindControl<ResourceTotalsRow>("TotalsRow");
        if (totalsRow is not null)
            totalsRow.CopyClicked += OnCopyScreenshotClick;

        WireContextMenu();
        InitializeUsagePanel();
    }

    /// <summary>
    /// Wires up the Claude usage panel (right/bottom of the window).
    /// On Windows this creates a NativeWebView pointing at
    /// claude.ai/settings/usage; on Linux it's a no-op (the panel
    /// stays an empty ContentControl). Implemented as a partial
    /// method so the Linux build doesn't need to reference the
    /// WebView2 package. The Windows implementation lives in
    /// MainWindow.WebView.cs which is only compiled when
    /// '$(OS)' == 'Windows_NT'.
    /// </summary>
    partial void InitializeUsagePanel();

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

        Grid.SetRow(LauncherPanel, 0);   Grid.SetColumn(LauncherPanel, launcherCol);
        Grid.SetRow(PanelSplitter, 0);   Grid.SetColumn(PanelSplitter, 1);
        Grid.SetRow(UsagePanelHost, 0);  Grid.SetColumn(UsagePanelHost, usageCol);

        PanelSplitter.ResizeDirection = GridResizeDirection.Columns;
    }

    private void ApplyVerticalLayout()
    {
        MainGrid.ColumnDefinitions = new ColumnDefinitions("*");
        MainGrid.RowDefinitions = new RowDefinitions("*,4,320");

        Grid.SetRow(LauncherPanel, 0);   Grid.SetColumn(LauncherPanel, 0);
        Grid.SetRow(PanelSplitter, 1);   Grid.SetColumn(PanelSplitter, 0);
        Grid.SetRow(UsagePanelHost, 2);  Grid.SetColumn(UsagePanelHost, 0);

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
