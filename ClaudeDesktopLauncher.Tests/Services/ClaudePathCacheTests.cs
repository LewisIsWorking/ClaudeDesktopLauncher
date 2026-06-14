using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class ClaudePathCacheTests
{
    private readonly IClaudePathResolver _resolver = Substitute.For<IClaudePathResolver>();
    private ClaudePathCache CreateSut() => new(_resolver);

    [Fact]
    public void GetPath_OnFirstCall_ResolvesAndCachesPath()
    {
        _resolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");

        var result = CreateSut().GetPath();

        Assert.Equal(@"C:\claude.exe", result);
        _resolver.Received(1).ResolveClaudeExePath();
    }

    [Fact]
    public void GetPath_OnSubsequentCall_UsesCachedValue()
    {
        _resolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        var sut = CreateSut();

        sut.GetPath();
        sut.GetPath();

        _resolver.Received(1).ResolveClaudeExePath();
    }

    [Fact]
    public void Refresh_ForcesNewResolution()
    {
        _resolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");
        var sut = CreateSut();

        sut.GetPath();
        sut.Refresh();
        sut.GetPath();

        _resolver.Received(2).ResolveClaudeExePath();
    }

    [Fact]
    public void IsClaudeInstalled_WhenPathFound_ReturnsTrue()
    {
        _resolver.ResolveClaudeExePath().Returns(@"C:\claude.exe");

        Assert.True(CreateSut().IsClaudeInstalled());
    }

    [Fact]
    public void IsClaudeInstalled_WhenPathNull_ReturnsFalse()
    {
        _resolver.ResolveClaudeExePath().Returns((string?)null);

        Assert.False(CreateSut().IsClaudeInstalled());
    }
}
