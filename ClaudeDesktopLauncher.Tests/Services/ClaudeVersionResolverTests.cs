using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudeVersionResolverTests
{
    private readonly IClaudePathCache _pathCache = Substitute.For<IClaudePathCache>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    private ClaudeVersionResolver CreateSut() =>
        new(_pathCache, _fileSystem, _logger);

    [Fact]
    public void GetClaudeVersion_WhenPathCacheReturnsNull_ReturnsNull()
    {
        _pathCache.GetPath().Returns((string?)null);

        Assert.Null(CreateSut().GetClaudeVersion());
        _fileSystem.DidNotReceiveWithAnyArgs().GetFileProductVersion(default!);
    }

    [Fact]
    public void GetClaudeVersion_WhenFileSystemReturnsNull_ReturnsNullAndLogsWarning()
    {
        _pathCache.GetPath().Returns(@"C:\path\to\claude.exe");
        _fileSystem.GetFileProductVersion(@"C:\path\to\claude.exe").Returns((string?)null);

        Assert.Null(CreateSut().GetClaudeVersion());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("unreadable")), Arg.Any<string>());
    }

    [Fact]
    public void GetClaudeVersion_WhenFileSystemReturnsVersion_ReturnsIt()
    {
        _pathCache.GetPath().Returns(@"C:\path\to\claude.exe");
        _fileSystem.GetFileProductVersion(@"C:\path\to\claude.exe").Returns("1.3109.0.0");

        Assert.Equal("1.3109.0.0", CreateSut().GetClaudeVersion());
    }

    [Fact]
    public void GetClaudeVersion_LogsDebugWhenPathCacheEmpty()
    {
        _pathCache.GetPath().Returns((string?)null);

        CreateSut().GetClaudeVersion();

        _logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("unresolved")), Arg.Any<string>());
    }
}