using System.Text.Json;
using ClaudeDesktopLauncher.Core.Models;
using ClaudeDesktopLauncher.Core.Services;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using NSubstitute;

namespace ClaudeDesktopLauncher.Tests.Services;

public class SettingsServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private SettingsService CreateSut() => new(_fileSystem);

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        Assert.Equal(3, CreateSut().Load().DefaultSlotCount);
    }

    [Fact]
    public void Load_WhenFileExists_ReturnsDeserializedSettings()
    {
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);
        _fileSystem.ReadAllText(Arg.Any<string>())
            .Returns(JsonSerializer.Serialize(new AppSettings { DefaultSlotCount = 5 }));

        Assert.Equal(5, CreateSut().Load().DefaultSlotCount);
    }

    [Fact]
    public void Load_WhenFileContainsInvalidJson_ReturnsDefaults()
    {
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);
        _fileSystem.ReadAllText(Arg.Any<string>()).Returns("not-valid-json");

        Assert.Equal(3, CreateSut().Load().DefaultSlotCount);
    }

    [Fact]
    public void Save_WritesSerializedSettingsToFile()
    {
        CreateSut().Save(new AppSettings { DefaultSlotCount = 7 });

        _fileSystem.Received(1).WriteAllText(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("7")));
    }

    [Fact]
    public void Save_CreatesDirectoryBeforeWriting()
    {
        CreateSut().Save(new AppSettings());

        _fileSystem.Received(1).CreateDirectory(Arg.Any<string>());
    }
}
