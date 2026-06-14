using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;
using ComeOnOverDesktopLauncher.Services.Interfaces;
using ComeOnOverDesktopLauncher.ViewModels;
using NSubstitute;
namespace ComeOnOverDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Shared test fixture for <see cref="MainWindowViewModel"/>. Encapsulates
/// the mock graph and the SUT construction so test classes stay focused on
/// assertions rather than wiring boilerplate.
///
/// Each fixture is an independent mock graph - instantiate a fresh one per
/// test class (xUnit creates a new test-class instance per <c>[Fact]</c>
/// so fields are naturally isolated).
///
/// Slot and external list VMs are constructed as real instances (not
/// mocks) with substituted dependencies so tests can exercise the real
/// filter/reconcile pipeline through the VM.
/// </summary>
public class MainWindowViewModelTestFixture
{
    public IClaudeInstanceLauncher Launcher { get; } = Substitute.For<IClaudeInstanceLauncher>();
    public ISlotInitialiser SlotInitialiser { get; } = Substitute.For<ISlotInitialiser>();
    public ISettingsService SettingsService { get; } = Substitute.For<ISettingsService>();
    public IClaudePathResolver PathResolver { get; } = Substitute.For<IClaudePathResolver>();
    public IResourceMonitor ResourceMonitor { get; } = Substitute.For<IResourceMonitor>();
    public IStartupService StartupService { get; } = Substitute.For<IStartupService>();
    public IVersionProvider VersionProvider { get; } = Substitute.For<IVersionProvider>();
    public IAutoUpdateService AutoUpdateService { get; } = Substitute.For<IAutoUpdateService>();
    public IUpdateApplyFailureDetector ApplyFailureDetector { get; } = Substitute.For<IUpdateApplyFailureDetector>();
    public IClaudeVersionResolver ClaudeVersionResolver { get; } = Substitute.For<IClaudeVersionResolver>();
    public IProcessService ProcessService { get; } = Substitute.For<IProcessService>();
    public IWindowThumbnailService ThumbnailService { get; } = Substitute.For<IWindowThumbnailService>();
    public IThumbnailPreviewService PreviewService { get; } = Substitute.For<IThumbnailPreviewService>();
    public IWindowHider WindowHider { get; } = Substitute.For<IWindowHider>();
    public IWindowShower WindowShower { get; } = Substitute.For<IWindowShower>();
    public IClaudeDiskUsageService DiskUsage { get; } = Substitute.For<IClaudeDiskUsageService>();
    public ILoggingService Logger { get; } = Substitute.For<ILoggingService>();
    public IClaudeProcessScanner Scanner { get; } = Substitute.For<IClaudeProcessScanner>();
    public IClaudeProcessClassifier Classifier { get; } = Substitute.For<IClaudeProcessClassifier>();
    public IConfirmDialogService ConfirmDialog { get; } = Substitute.For<IConfirmDialogService>();

    /// <summary>
    /// Builds a fresh <see cref="MainWindowViewModel"/>. Applies the two
    /// defaults that every test needs (settings load returns a valid
    /// <see cref="AppSettings"/>, version provider returns a known
    /// string); individual tests override mock behaviour as needed before
    /// calling this.
    /// </summary>
    public MainWindowViewModel CreateSut(AppSettings? settings = null)
    {
        SettingsService.Load().Returns(settings ?? new AppSettings { DefaultSlotCount = 2 });
        VersionProvider.GetVersion().Returns("1.3.0");
        AutoUpdateService.CheckForUpdatesAsync()
            .Returns(Task.FromResult(new UpdateCheckResult(UpdateStatus.NoUpdateAvailable)));

        var slotInstances = new SlotInstanceListViewModel(
            Scanner, Classifier, SlotInitialiser, Logger);
        var externalInstances = new ExternalInstanceListViewModel(
            Scanner, Classifier, ConfirmDialog, ProcessService, Logger);

        return new MainWindowViewModel(
            Launcher,
            SettingsService, PathResolver, ResourceMonitor,
            StartupService, AutoUpdateService, ApplyFailureDetector, VersionProvider,
            ClaudeVersionResolver, ProcessService, ThumbnailService, PreviewService,
            WindowHider, WindowShower, DiskUsage, slotInstances, externalInstances, Logger);
    }
}
