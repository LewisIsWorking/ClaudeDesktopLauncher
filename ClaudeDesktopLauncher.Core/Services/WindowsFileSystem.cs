using System.Diagnostics;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Production filesystem implementation using System.IO.
/// </summary>
public class WindowsFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string[] GetDirectories(string path, string searchPattern) =>
        Directory.GetDirectories(path, searchPattern);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string content) =>
        File.WriteAllText(path, content);

    public void AppendAllText(string path, string content) =>
        File.AppendAllText(path, content);

    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public long GetFileSize(string path) =>
        new FileInfo(path).Length;

    public void CopyFile(string sourcePath, string destinationPath) =>
        File.Copy(sourcePath, destinationPath, overwrite: true);

    public void CopyFileSharedRead(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var dest = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(dest);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    public string? GetFileProductVersion(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return FileVersionInfo.GetVersionInfo(path).ProductVersion;
        }
        catch (FileNotFoundException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public byte[] ReadFileHeader(string path, int byteCount)
    {
        if (!File.Exists(path)) return Array.Empty<byte>();
        using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[byteCount];
        var read = fs.Read(buffer, 0, byteCount);
        if (read == byteCount) return buffer;
        var trimmed = new byte[read];
        Array.Copy(buffer, trimmed, read);
        return trimmed;
    }
}