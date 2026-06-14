using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Tests covering the <c>IsSeeded</c> heuristic and the seed-source
/// precedence order inside <c>EnsureInitialised</c>: seed cache first,
/// then default-profile copy, then sibling-slot copy, and the v1.7.1
/// behaviour of re-seeding even when the slot already appears seeded
/// (so Cookies/LocalState pairs don't drift between sessions).
/// </summary>
public class SlotInitialiserSourceOrderingTests
{
    private readonly SlotInitialiserTestFixture _f = new();

    [Fact]
    public void IsSeeded_WhenCookiesFileDoesNotExist_ReturnsFalse()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        Assert.False(_f.CreateSut().IsSeeded(_f.Slot));
    }

    [Fact]
    public void IsSeeded_WhenCookiesFileIsMinimal_ReturnsFalse()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.SlotCookiesPath).Returns(20480L);
        Assert.False(_f.CreateSut().IsSeeded(_f.Slot));
    }

    [Fact]
    public void IsSeeded_WhenCookiesFileHasRealData_ReturnsTrue()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.SlotCookiesPath).Returns(36864L);
        Assert.True(_f.CreateSut().IsSeeded(_f.Slot));
    }

    [Fact]
    public void EnsureInitialised_WhenSeedCacheCanSeed_UsesCacheAndSkipsOtherSources()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.SeedCache.TrySeed(_f.Slot).Returns(true);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.SeedCache.Received(1).TrySeed(_f.Slot);
        _f.FileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenSeedCacheFails_FallsThroughToDefaultProfile()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16).Returns(SlotInitialiserTestFixture.ValidSqliteHeader);
        _f.SeedCache.TrySeed(_f.Slot).Returns(false);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.SeedCache.Received(1).TrySeed(_f.Slot);
        _f.FileSystem.Received(1).CopyFileSharedRead(_f.DefaultCookiesPath, _f.SlotCookiesPath);
    }

    [Fact]
    public void EnsureInitialised_WhenAlreadySeededAndCacheUnavailable_DoesNotCopy()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.SlotCookiesPath).Returns(36864L);
        _f.SeedCache.TrySeed(_f.Slot).Returns(false);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_WhenAlreadySeededButCachePopulated_ReSeedsFromCache()
    {
        // Even when the slot already has a populated cookies file, re-seed
        // from the cache so any stale/mismatched Cookies-vs-LocalState pair
        // is corrected before launch. This prevents the surprise login wall
        // seen in v1.7.0 when a slot's data dir had cookies from one session
        // and Local State from another.
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(true);
        _f.FileSystem.GetFileSize(_f.SlotCookiesPath).Returns(36864L);
        _f.SeedCache.TrySeed(_f.Slot).Returns(true);

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.SeedCache.Received(1).TrySeed(_f.Slot);
        _f.FileSystem.DidNotReceive().CopyFileSharedRead(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void EnsureInitialised_CreatesNetworkDirectoryBeforeCopying()
    {
        _f.FileSystem.FileExists(_f.SlotCookiesPath).Returns(false);
        _f.FileSystem.FileExists(_f.DefaultCookiesPath).Returns(true);
        _f.FileSystem.ReadFileHeader(_f.SlotCookiesPath, 16).Returns(SlotInitialiserTestFixture.ValidSqliteHeader);

        var networkDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1", "Network");

        _f.CreateSut().EnsureInitialised(_f.Slot);

        _f.FileSystem.Received(1).CreateDirectory(networkDir);
    }
}
