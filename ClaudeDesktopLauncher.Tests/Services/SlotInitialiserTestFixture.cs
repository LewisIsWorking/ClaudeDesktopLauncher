using System.Text;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

/// <summary>
/// Shared mock graph and path helpers for <c>SlotInitialiser</c> tests.
/// Extracted so the ordering, validation and failure-path test classes
/// can stay under 200 lines without duplicating path plumbing.
/// </summary>
public class SlotInitialiserTestFixture
{
    public IFileSystem FileSystem { get; } = Substitute.For<IFileSystem>();
    public ILoggingService Logger { get; } = Substitute.For<ILoggingService>();
    public ISlotSeedCache SeedCache { get; } = Substitute.For<ISlotSeedCache>();
    public LaunchSlot Slot { get; } = new(1);

    public SlotInitialiser CreateSut() => new(FileSystem, Logger, SeedCache);

    public static readonly byte[] ValidSqliteHeader =
        Encoding.ASCII.GetBytes("SQLite format 3\0");
    public static readonly byte[] InvalidHeader = new byte[16];

    public string SlotCookiesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot1", "Network", "Cookies");

    public string DefaultCookiesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "Network", "Cookies");

    public string Slot2CookiesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeSlot2", "Network", "Cookies");
}
