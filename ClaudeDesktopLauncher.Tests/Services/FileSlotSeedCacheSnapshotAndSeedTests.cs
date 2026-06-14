using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static ClaudeDesktopLauncher.Tests.Services.FileSlotSeedCacheTestFixtures;

namespace ClaudeDesktopLauncher.Tests.Services;

public class FileSlotSeedCacheSnapshotAndSeedTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly ILoggingService _logger = Substitute.For<ILoggingService>();
    private readonly LaunchSlot _slot = new(1);

    [Fact]
    public void TrySnapshot_HappyPath_CopiesAllThreeFiles()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);

        var result = CreateSut(_fs, _logger).TrySnapshot(_slot);

        Assert.True(result);
        _fs.Received(1).CopyFileSharedRead(SlotCookies, CacheCookies);
        _fs.Received(1).CopyFileSharedRead(SlotLocalState, CacheLocalState);
        _fs.Received(1).CopyFileSharedRead(SlotPreferences, CachePreferences);
    }

    [Fact]
    public void TrySnapshot_WhenCookiesSourceMissing_ReturnsFalse()
    {
        _fs.FileExists(SlotCookies).Returns(false);
        Assert.False(CreateSut(_fs, _logger).TrySnapshot(_slot));
    }

    [Fact]
    public void TrySnapshot_WhenCookiesCopyThrows_ReturnsFalse()
    {
        _fs.FileExists(SlotCookies).Returns(true);
        _fs.When(f => f.CopyFileSharedRead(SlotCookies, CacheCookies))
            .Throw(new IOException("locked"));
        Assert.False(CreateSut(_fs, _logger).TrySnapshot(_slot));
    }

    [Fact]
    public void TrySnapshot_WhenCookiesMagicInvalid_DiscardsAndReturnsFalse()
    {
        _fs.FileExists(SlotCookies).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(InvalidHeader);

        var result = CreateSut(_fs, _logger).TrySnapshot(_slot);

        Assert.False(result);
        _fs.Received(1).DeleteFile(CacheCookies);
    }

    [Fact]
    public void TrySnapshot_WhenLocalStateMissingKey_RollsBackCookies()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(NoKeyLocalStateJson);

        var result = CreateSut(_fs, _logger).TrySnapshot(_slot);

        Assert.False(result);
        _fs.Received(1).DeleteFile(CacheCookies);
        _fs.Received(1).DeleteFile(CacheLocalState);
    }

    [Fact]
    public void TrySnapshot_WhenLocalStateCopyThrows_RollsBackCookies()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.When(f => f.CopyFileSharedRead(SlotLocalState, CacheLocalState))
            .Throw(new IOException("locked"));

        var result = CreateSut(_fs, _logger).TrySnapshot(_slot);

        Assert.False(result);
        _fs.Received(1).DeleteFile(CacheCookies);
    }

    [Fact]
    public void TrySnapshot_WhenPreferencesCopyThrows_RollsBackBothPriorFiles()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);
        _fs.When(f => f.CopyFileSharedRead(SlotPreferences, CachePreferences))
            .Throw(new IOException("locked"));

        var result = CreateSut(_fs, _logger).TrySnapshot(_slot);

        Assert.False(result);
        _fs.Received(1).DeleteFile(CacheCookies);
        _fs.Received(1).DeleteFile(CacheLocalState);
    }

    [Fact]
    public void TrySnapshot_WhenCleanupDeleteThrows_StillReturnsFalseWithoutThrowing()
    {
        _fs.FileExists(SlotCookies).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(InvalidHeader);
        _fs.When(f => f.DeleteFile(CacheCookies)).Throw(new IOException("gone"));

        bool result = false;
        Assert.Null(Record.Exception(() => result = CreateSut(_fs, _logger).TrySnapshot(_slot)));
        Assert.False(result);
    }

    [Fact]
    public void TrySeed_WhenCacheNotPopulated_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);
        Assert.False(CreateSut(_fs, _logger).TrySeed(_slot));
    }

    [Fact]
    public void TrySeed_WhenCachePopulated_CopiesAllThreeFilesToSlot()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);

        var result = CreateSut(_fs, _logger).TrySeed(_slot);

        Assert.True(result);
        var slotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1");
        _fs.Received(1).CreateDirectory(Path.Combine(slotDir, "Network"));
        _fs.Received(1).CopyFile(CacheCookies, Path.Combine(slotDir, "Network", "Cookies"));
        _fs.Received(1).CopyFile(CacheLocalState, Path.Combine(slotDir, "Local State"));
        _fs.Received(1).CopyFile(CachePreferences, Path.Combine(slotDir, "Preferences"));
    }

    [Fact]
    public void TrySeed_WhenCopyThrows_ReturnsFalse()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(true);
        _fs.ReadFileHeader(CacheCookies, 16).Returns(ValidSqliteHeader);
        _fs.ReadAllText(CacheLocalState).Returns(ValidLocalStateJson);
        _fs.When(f => f.CopyFile(Arg.Any<string>(), Arg.Any<string>()))
            .Throw(new IOException("target locked"));

        Assert.False(CreateSut(_fs, _logger).TrySeed(_slot));
    }
}