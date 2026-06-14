using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Tests covering the SQLite-validation and resilience behaviour of
/// <c>SlotInitialiser.EnsureInitialised</c>: copies that land but
/// produce invalid bytes get discarded and the next source tried; copy
/// failures don't propagate; missing sources are silently skipped; and
/// cleanup errors during invalid-copy discard don't crash the caller.
/// The direct "copy from default profile when slot has no cookies"
/// happy-path lives here too since it's one of the validation cases.
/// </summary>
public class SlotInitialiserFallbackTests
{
    private readonly SlotInitialiserTestFixture _f = new();

    [Fact]
    public void EnsureInitialised_WhenDefaultExistsAndCopyValid_CopiesFromDefault()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16).Returns(SlotInitialiserTestFixture.ValidSqliteHeader);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.Received(1).CopyFileSharedRead(_f.DefaultCookiesPath, _f.SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenCopyProducesInvalidSqlite_DiscardsAndTriesFallback()
    {
        // Default copies but produces invalid bytes; slot 2 exists & is seeded; its copy is valid
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.FileExists(_f.Slot2CookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.Slot2CookiesPath).Returns(36864L);

        // First header check (after default copy) -> invalid; second (after fallback copy) -> valid
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16)
            .Returns(SlotInitialiserTestFixture.InvalidHeader, SlotInitialiserTestFixture.ValidSqliteHeader);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.Received(1).CopyFileSharedRead(_f.DefaultCookiesPath, _f.SlotCookiesPath);
        _f.FileSystem.Received(1).CopyFileSharedRead(_f.Slot2CookiesPath, _f.SlotCookiesPath);
        _f.FileSystem.Received(1).DeleteFile(_f.SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenDefaultLockedAndFallbackSeeded_CopiesFromFallback()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.When(fs => fs.CopyFileSharedRead(_f.DefaultCookiesPath, Arg.Any<string>()))
            .Throw(new IOException("File locked"));

        _f.FileSystem.FileExists(_f.Slot2CookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.Slot2CookiesPath).Returns(36864L);
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16).Returns(SlotInitialiserTestFixture.ValidSqliteHeader);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.Received(1).CopyFileSharedRead(_f.Slot2CookiesPath, _f.SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenNoSourceExists_DoesNotCopy()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(Arg.Any<string>()).Returns(false);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenAllCopiesFail_DoesNotThrow()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(Arg.Any<string>()).Returns(true);
        _f.FileSystem.GetFileSize(Arg.Any<string>()).Returns(20480L);
        _f.FileSystem.When(fs => fs.CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>()))
            .Throw(new IOException("File locked"));

        Assert.Null(Record.Exception(() => _f.CreateSut().EnsureInitialised(_f.Slot)));
    }

    [Fact]
    public void EnsureInitialised_WhenDeleteOfInvalidCopyThrows_ContinuesGracefully()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16).Returns(SlotInitialiserTestFixture.InvalidHeader);
        _f.FileSystem.When(fs => fs.DeleteFile(_f.SlotCookiesPath))
            .Throw(new IOException("locked"));

        Assert.Null(Record.Exception(() => _f.CreateSut().EnsureInitialised(_f.Slot)));
    }
}
