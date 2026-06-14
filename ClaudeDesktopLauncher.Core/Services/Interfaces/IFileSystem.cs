namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Abstracts filesystem operations to allow unit testing without real disk access.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string[] GetDirectories(string path, string searchPattern);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void AppendAllText(string path, string content);
    void CreateDirectory(string path);
    long GetFileSize(string path);
    void CopyFile(string sourcePath, string destinationPath);

    /// <summary>
    /// Copies a file while tolerating a concurrent writer that holds an exclusive
    /// lock under the default share mode. Opens the source with
    /// <c>FileShare.ReadWrite</c> so we can read cookies DBs that Chromium has open.
    /// Destination is overwritten.
    /// </summary>
    void CopyFileSharedRead(string sourcePath, string destinationPath);

    /// <summary>
    /// Deletes a file if it exists. No-op if it does not. Never throws for missing
    /// files - other IO errors (permissions etc.) propagate.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Reads the ProductVersion from a Windows PE file (exe/dll) via
    /// FileVersionInfo. Returns null if the file does not exist, has no
    /// version resource, or cannot be opened. Never throws for these
    /// expected cases.
    /// </summary>
    string? GetFileProductVersion(string path);

    /// <summary>
    /// Reads the first <paramref name="byteCount"/> bytes of a file.
    /// Returns fewer bytes if the file is shorter; returns an empty array if the
    /// file does not exist. Used to inspect file magic numbers (SQLite header etc.).
    /// </summary>
    byte[] ReadFileHeader(string path, int byteCount);
}