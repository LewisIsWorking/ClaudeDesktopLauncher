using System.Runtime.CompilerServices;

namespace ClaudeDesktopLauncher.Core.Services.Interfaces;

/// <summary>
/// Diagnostic logging abstraction.
/// Callers do not need to pass <paramref name="caller"/> - it is auto-populated
/// with the name of the calling method via <see cref="CallerMemberNameAttribute"/>.
/// Implementations must never throw - a logging failure should not crash the app.
/// </summary>
public interface ILoggingService
{
    void LogInfo(string message, [CallerMemberName] string caller = "");

    void LogWarning(string message, [CallerMemberName] string caller = "");

    void LogError(
        string message,
        Exception? exception = null,
        [CallerMemberName] string caller = "");

    void LogDebug(string message, [CallerMemberName] string caller = "");

    /// <summary>
    /// Absolute path to the directory where log files are written.
    /// Used by the UI "Open Logs" command.
    /// </summary>
    string GetLogDirectory();
}
