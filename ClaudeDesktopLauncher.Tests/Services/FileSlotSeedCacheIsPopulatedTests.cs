using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static ClaudeDesktopLauncher.Tests.Services.FileSlotSeedCacheTestFixtures;

namespace ClaudeDesktopLauncher.Tests.Services;

public class FileSlotSeedCacheIsPopulatedTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();

    [Fact]
    public void Ctor_CreatesCacheDirectoryIfMissing()
    {
        _fs.DirectoryExists(CacheDir).Returns(false);
        _ = CreateSut(_fs, _logger);
        _fs.Received(1).CreateDirectory(CacheDir);
    }

    [Fact]
    public void Ctor_DoesNotCreateDirectoryIfPresent()
    {
        _fs.DirectoryExists(CacheDir).Returns(true);
        _ = CreateSut(_fs, _logger);
        _fs.DidNotReceive().CreateDirectory(CacheDir);
    }

    [Fact]
    public void Ctor_SwallowsIoExceptionDuringDirectoryCreate()
    {
        _fs.DirectoryExists(CacheDir).Returns(false);
        _fs.When(f => f.CreateDirectory(CacheDir))
            .Throw(new IOException("disk full"));
        Assert.Null(Record.Exception(() => CreateSut(_fs, _logger)));
    }

    [Fact]
    public void IsPopulated_WhenAnyFileMissing_ReturnsFalse()
    {
        _fs.FileExists(CacheCookies).Returns(true);
        _fs.FileExists(CacheLocalState).Returns(true);
        _fs.FileExists(CachePreferences).Returns(false);
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenCookiesHaveBadMagic_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(InvalidHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenLocalStateMissingKey_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(NoKeyLocalStateJson);
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenLocalStateHasEmptyKey_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(EmptyKeyLocalStateJson);
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenLocalStateMalformed_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(MalformedJson);
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenLocalStateReadThrows_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Throws(new IOException("locked"));
        Assert.False(CreateSut(_fs, _logger).IsPopulated);
    }

    [Fact]
    public void IsPopulated_WhenEverythingValid_ReturnsTrue()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);
        Assert.True(CreateSut(_fs, _logger).IsPopulated);
    }
}