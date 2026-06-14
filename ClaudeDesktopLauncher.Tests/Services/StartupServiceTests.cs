using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class StartupServiceTests
{
    private readonly IRegistryService _registry = Substitute.For<IRegistryService>();
    private StartupService CreateSut() => new(_registry);

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClaudeDesktopLauncher";

    [Fact]
    public void IsStartupEnabled_WhenValueExists_ReturnsTrue()
    {
        _registry.GetValue(RunKeyPath, AppName).Returns("\"C:\\app.exe\" --minimised");

        Assert.True(CreateSut().IsStartupEnabled());
    }

    [Fact]
    public void IsStartupEnabled_WhenValueDoesNotExist_ReturnsFalse()
    {
        _registry.GetValue(RunKeyPath, AppName).Returns((string?)null);

        Assert.False(CreateSut().IsStartupEnabled());
    }

    [Fact]
    public void EnableStartup_WritesCorrectRegistryValue()
    {
        CreateSut().EnableStartup(@"C:\app.exe");

        _registry.Received(1).SetValue(
            RunKeyPath,
            AppName,
            Arg.Is<string>(v => v.Contains("app.exe") && v.Contains("--minimised")));
    }

    [Fact]
    public void DisableStartup_DeletesRegistryValue()
    {
        CreateSut().DisableStartup();

        _registry.Received(1).DeleteValue(RunKeyPath, AppName);
    }
}
