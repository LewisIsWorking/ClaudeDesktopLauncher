using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClaudeDesktopLauncher.Core.Services.Interfaces;
using ClaudeDesktopLauncher.Services.Interfaces;

namespace ClaudeDesktopLauncher.Services;

/// <summary>
/// Windows implementation of <see cref="IShellLinkWriter"/> that
/// creates <c>.lnk</c> shortcut files via the <c>WScript.Shell</c>
/// COM API. This is the same API Velopack itself uses internally,
/// so shortcuts produced here are indistinguishable from a
/// freshly-installed Velopack shortcut from the Shell's perspective.
///
/// <para>
/// COM objects created here are released eagerly via
/// <see cref="Marshal.ReleaseComObject"/> so the process doesn't
/// leak RCW handles across repeated heal cycles (unlikely in
/// practice because <see cref="IShortcutHealer.HealIfMissing"/>
/// runs once per launch, but correctness matters).
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public class WScriptShellLinkWriter : IShellLinkWriter
{
    private readonly ILoggingService _logger;

    public WScriptShellLinkWriter(ILoggingService logger) => _logger = logger;

    public bool TryCreateShortcut(string lnkPath, string targetExePath, string description)
    {
        try
        {
            var parent = Path.GetDirectoryName(lnkPath);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
                _logger.LogInfo($"Created shortcut parent directory: {parent}");
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException(
                    "WScript.Shell ProgID not registered on this machine.");
            var shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException(
                    "WScript.Shell Activator.CreateInstance returned null.");

            try
            {
                dynamic link = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    binder: null,
                    target: shell,
                    args: new object[] { lnkPath })
                    ?? throw new InvalidOperationException(
                        "CreateShortcut returned null.");

                try
                {
                    link.TargetPath = targetExePath;
                    link.WorkingDirectory = Path.GetDirectoryName(targetExePath) ?? string.Empty;
                    link.IconLocation = $"{targetExePath},0";
                    link.Description = description;
                    link.Save();
                }
                finally
                {
                    Marshal.ReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }

            _logger.LogInfo($"Created shortcut: {lnkPath} -> {targetExePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                $"Failed to create shortcut at '{lnkPath}' -> '{targetExePath}': {ex.Message}");
            return false;
        }
    }
}
