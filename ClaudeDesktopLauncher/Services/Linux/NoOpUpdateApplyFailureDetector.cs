using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services.Linux;

/// <summary>
/// Linux stub IUpdateApplyFailureDetector: always returns false. The
/// Windows detector tails Velopack's log for recent apply failures;
/// since the Linux build doesn't use Velopack auto-update, there is
/// no log to tail and no failure to detect.
/// </summary>
public class NoOpUpdateApplyFailureDetector : IUpdateApplyFailureDetector
{
    public bool ApplyFailedRecently(TimeSpan recentWindow) => false;
}
