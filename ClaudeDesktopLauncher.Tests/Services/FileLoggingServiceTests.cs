using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class FileLoggingServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private const string LogDir = @"C:\test\logs";

    private FileLoggingService CreateSut() => new(_fileSystem, LogDir);

    [Fact]
    public void Constructor_WhenLogDirectoryMissing_CreatesIt()
    {
        _fileSystem.DirectoryExists(LogDir).Returns(false);

        _ = CreateSut();

        _fileSystem.Received(1).CreateDirectory(LogDir);
    }

    [Fact]
    public void Constructor_WhenLogDirectoryExists_DoesNotCreateIt()
    {
        _fileSystem.DirectoryExists(LogDir).Returns(true);

        _ = CreateSut();

        _fileSystem.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public void Constructor_WhenDirectoryCreationThrows_DoesNotPropagate()
    {
        _fileSystem.DirectoryExists(LogDir).Returns(false);
        _fileSystem.When(f => f.CreateDirectory(LogDir))
            .Do(_ => throw new UnauthorizedAccessException("denied"));

        var ex = Record.Exception(() => CreateSut());

        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_DefaultLogDirectory_ContainsLogsFolder()
    {
        var sut = new FileLoggingService(_fileSystem);

        Assert.Contains("logs", sut.GetLogDirectory());
        Assert.Contains("ClaudeDesktopLauncher", sut.GetLogDirectory());
    }

    [Fact]
    public void GetLogDirectory_ReturnsConfiguredDirectory()
    {
        Assert.Equal(LogDir, CreateSut().GetLogDirectory());
    }

    [Fact]
    public void LogInfo_WritesInfoLevelLine()
    {
        CreateSut().LogInfo("hello");

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("[INFO]") && s.Contains("hello")));
    }

    [Fact]
    public void LogWarning_WritesWarnLevelLine()
    {
        CreateSut().LogWarning("careful");

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("[WARN]") && s.Contains("careful")));
    }

    [Fact]
    public void LogDebug_WritesDebugLevelLine()
    {
        CreateSut().LogDebug("trace");

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("[DEBUG]") && s.Contains("trace")));
    }

    [Fact]
    public void LogError_WithoutException_WritesErrorLevelLine()
    {
        CreateSut().LogError("boom");

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("[ERROR]") && s.Contains("boom")));
    }

    [Fact]
    public void LogError_WithException_IncludesExceptionDetails()
    {
        var exception = new InvalidOperationException("inner detail");

        CreateSut().LogError("boom", exception);

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s =>
                s.Contains("boom") &&
                s.Contains("InvalidOperationException") &&
                s.Contains("inner detail")));
    }

    [Fact]
    public void Log_WritesToDatedFilePath()
    {
        CreateSut().LogInfo("x");

        var expectedFileName = $"launcher-{DateTime.Now:yyyy-MM-dd}.log";
        _fileSystem.Received(1).AppendAllText(
            Arg.Is<string>(p => p.EndsWith(expectedFileName)),
            Arg.Any<string>());
    }

    [Fact]
    public void Log_IncludesCallerMemberName()
    {
        CreateSut().LogInfo("x", caller: "MyMethod");

        _fileSystem.Received(1).AppendAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("[MyMethod]")));
    }

    [Fact]
    public void Log_WhenAppendThrows_DoesNotPropagate()
    {
        _fileSystem.When(f => f.AppendAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new IOException("disk full"));

        var sut = CreateSut();

        Assert.Null(Record.Exception(() => sut.LogInfo("x")));
        Assert.Null(Record.Exception(() => sut.LogWarning("x")));
        Assert.Null(Record.Exception(() => sut.LogError("x")));
        Assert.Null(Record.Exception(() => sut.LogDebug("x")));
    }

    [Fact]
    public void Log_ConcurrentWrites_AllSucceed()
    {
        var sut = CreateSut();

        Parallel.For(0, 50, i => sut.LogInfo($"msg {i}"));

        _fileSystem.Received(50).AppendAllText(Arg.Any<string>(), Arg.Any<string>());
    }
}
