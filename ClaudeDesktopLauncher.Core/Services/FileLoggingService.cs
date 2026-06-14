using System.Globalization;
using System.Text;
using ClaudeDesktopLauncher.Core.Services.Interfaces;

namespace ClaudeDesktopLauncher.Core.Services;

/// <summary>
/// Default <see cref="ILoggingService"/> that writes to rolling daily log files
/// at %APPDATA%\ClaudeDesktopLauncher\logs\launcher-yyyy-MM-dd.log.
/// Thread-safe via a single write lock.
/// All disk I/O is wrapped in try/catch; a failure to write a log line is silently swallowed
/// so that logging can never crash the app.
/// </summary>
public class FileLoggingService : ILoggingService
{
    private const string LogFolderName = "ClaudeDesktopLauncher";
    private const string LogSubfolderName = "logs";
    private const string LogFilePrefix = "launcher-";
    private const string LogFileExtension = ".log";
    private const string TimestampFormat = "HH:mm:ss.fff";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly object _writeLock = new();
    private readonly IFileSystem _fileSystem;
    private readonly string _logDirectory;

    public FileLoggingService(IFileSystem fileSystem)
        : this(fileSystem, DefaultLogDirectory()) { }

    // Overload exists for tests - lets them redirect logs to a temp directory
    // without touching the real AppData folder.
    public FileLoggingService(IFileSystem fileSystem, string logDirectory)
    {
        _fileSystem = fileSystem;
        _logDirectory = logDirectory;
        EnsureDirectoryExists();
    }

    public string GetLogDirectory() => _logDirectory;

    public void LogInfo(string message, string caller = "") =>
        Write("INFO", caller, message, null);

    public void LogWarning(string message, string caller = "") =>
        Write("WARN", caller, message, null);

    public void LogError(string message, Exception? exception = null, string caller = "") =>
        Write("ERROR", caller, message, exception);

    public void LogDebug(string message, string caller = "") =>
        Write("DEBUG", caller, message, null);

    private void Write(string level, string caller, string message, Exception? exception)
    {
        try
        {
            var line = Format(level, caller, message, exception);
            var path = CurrentLogFilePath();
            lock (_writeLock)
            {
                _fileSystem.AppendAllText(path, line);
            }
        }
        catch
        {
            // Swallowed intentionally - logging must never crash the caller.
        }
    }

    private static string Format(string level, string caller, string message, Exception? exception)
    {
        var sb = new StringBuilder();
        sb.Append('[')
          .Append(DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture))
          .Append("] [").Append(level).Append("] [").Append(caller).Append("] ")
          .AppendLine(message);

        if (exception is not null)
            sb.AppendLine(exception.ToString());

        return sb.ToString();
    }

    private string CurrentLogFilePath()
    {
        var date = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture);
        return Path.Combine(_logDirectory, $"{LogFilePrefix}{date}{LogFileExtension}");
    }

    private void EnsureDirectoryExists()
    {
        try
        {
            if (!_fileSystem.DirectoryExists(_logDirectory))
                _fileSystem.CreateDirectory(_logDirectory);
        }
        catch
        {
            // Swallowed - logging will fail silently rather than crash startup.
        }
    }

    private static string DefaultLogDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LogFolderName,
            LogSubfolderName);
}
