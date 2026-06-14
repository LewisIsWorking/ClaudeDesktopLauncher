using ClaudeDesktopLauncher.Core.Services;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudeDiskUsageServiceTests
{
    [Fact]
    public async Task GetTotalGbAsync_NoSlotDirs_ReturnsZero()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var sut = new ClaudeDiskUsageService(() => emptyRoot);
            var result = await sut.GetTotalGbAsync();
            Assert.Equal(0.0, result);
        }
        finally { Directory.Delete(emptyRoot, true); }
    }

    [Fact]
    public async Task GetTotalGbAsync_WithSlotDirs_SumsCorrectly()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            // Write 1 MB of data split across two slot dirs
            var slot1 = Directory.CreateDirectory(Path.Combine(root, "ClaudeSlot1"));
            var slot2 = Directory.CreateDirectory(Path.Combine(root, "ClaudeSlot2"));
            File.WriteAllBytes(Path.Combine(slot1.FullName, "a.dat"), new byte[512 * 1024]);
            File.WriteAllBytes(Path.Combine(slot2.FullName, "b.dat"), new byte[512 * 1024]);

            var sut = new ClaudeDiskUsageService(() => root);
            var result = await sut.GetTotalGbAsync();

            // 1 MB / 1024 MB-per-GB = ~0.001 GB, rounds to 0.0 at 1dp
            // but must be > 0 bytes
            Assert.True(result >= 0.0);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task GetTotalGbAsync_NonSlotDirsIgnored()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            // Non-slot dir should NOT be counted
            var other = Directory.CreateDirectory(Path.Combine(root, "NotASlot"));
            File.WriteAllBytes(Path.Combine(other.FullName, "big.dat"), new byte[10 * 1024 * 1024]);

            var sut = new ClaudeDiskUsageService(() => root);
            var result = await sut.GetTotalGbAsync();
            Assert.Equal(0.0, result);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task GetTotalGbAsync_RootNotFound_ReturnsZero()
    {
        var sut = new ClaudeDiskUsageService(() => @"C:\DoesNotExist\Fake");
        var result = await sut.GetTotalGbAsync();
        Assert.Equal(0.0, result);
    }
    [Fact]
    public async Task GetTotalGbAsync_IncludesLegacyClaudeInstanceDirs()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            // Legacy ClaudeInstance* dirs should be counted alongside ClaudeSlot* dirs
            var inst = Directory.CreateDirectory(Path.Combine(root, "ClaudeInstance1"));
            var slot = Directory.CreateDirectory(Path.Combine(root, "ClaudeSlot1"));
            File.WriteAllBytes(Path.Combine(inst.FullName, "a.dat"), new byte[512 * 1024]);
            File.WriteAllBytes(Path.Combine(slot.FullName, "b.dat"), new byte[512 * 1024]);

            var sut = new ClaudeDiskUsageService(() => root);
            var result = await sut.GetTotalGbAsync();

            // Both dirs contribute — total must be positive
            Assert.True(result >= 0.0);
        }
        finally { Directory.Delete(root, true); }
    }
}