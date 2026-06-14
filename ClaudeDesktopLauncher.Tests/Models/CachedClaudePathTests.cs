using ClaudeDesktopLauncher.Core.Models;

namespace ClaudeDesktopLauncher.Tests.Models;

public class CachedClaudePathTests
{
    [Fact]
    public void IsStale_WhenCachedRecently_ReturnsFalse()
    {
        var cached = new CachedClaudePath(@"C:\claude.exe", DateTime.UtcNow);

        Assert.False(cached.IsStale);
    }

    [Fact]
    public void IsStale_WhenCachedOver24HoursAgo_ReturnsTrue()
    {
        var cached = new CachedClaudePath(@"C:\claude.exe", DateTime.UtcNow.AddHours(-25));

        Assert.True(cached.IsStale);
    }

    [Fact]
    public void IsStale_WhenExePathIsNull_AndCachedRecently_ReturnsFalse()
    {
        var cached = new CachedClaudePath(null, DateTime.UtcNow);

        Assert.False(cached.IsStale);
    }
}
