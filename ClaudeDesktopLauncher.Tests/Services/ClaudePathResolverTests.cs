using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudePathResolverTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private ClaudePathResolver CreateSut() => new(_fileSystem, _logger);

    [Fact]
    public void ResolveClaudeExePath_WhenExeFound_ReturnsPath()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Returns([@"C:\Program Files\WindowsApps\Claude_1.0.0_x64"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);

        var result = CreateSut().ResolveClaudeExePath();

        Assert.NotNull(result);
        Assert.Contains("claude.exe", result);
    }

    [Fact]
    public void ResolveClaudeExePath_WhenNoDirFound_ReturnsNull()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Returns([]);

        var result = CreateSut().ResolveClaudeExePath();

        Assert.Null(result);
    }

    [Fact]
    public void ResolveClaudeExePath_WhenExeNotInDir_ReturnsNull()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Returns([@"C:\Program Files\WindowsApps\Claude_1.0.0_x64"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        var result = CreateSut().ResolveClaudeExePath();

        Assert.Null(result);
    }

    [Fact]
    public void ResolveClaudeExePath_WhenAccessDenied_ReturnsNull()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Throws(new UnauthorizedAccessException());

        var result = CreateSut().ResolveClaudeExePath();

        Assert.Null(result);
    }

    [Fact]
    public void IsClaudeInstalled_WhenExeFound_ReturnsTrue()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Returns([@"C:\Program Files\WindowsApps\Claude_1.0.0_x64"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);

        Assert.True(CreateSut().IsClaudeInstalled());
    }

    [Fact]
    public void IsClaudeInstalled_WhenExeNotFound_ReturnsFalse()
    {
        _fileSystem.GetDirectories(Arg.Any<string>(), Arg.Any<string>())
            .Returns([]);

        Assert.False(CreateSut().IsClaudeInstalled());
    }
}
