namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Thin abstraction over Windows' <c>WScript.Shell</c> COM API for
/// creating <c>.lnk</c> shortcut files. Introduced in v1.10.2 so
/// <see cref="IShortcutHealer"/> can be unit-tested without invoking
/// real COM interop in the test process.
///
/// <para>
/// Single method because the healer only needs to create shortcuts,
/// never read or modify existing ones (Velopack owns that path).
/// If future code needs richer shortcut handling (e.g. reading
/// the target of an existing .lnk), extend this interface rather
/// than reaching for <c>WScript.Shell</c> directly.
/// </para>
/// </summary>
public interface IShellLinkWriter
{
    /// <summary>
    /// Create a Windows shortcut file at <paramref name="lnkPath"/>
    /// pointing at <paramref name="targetExePath"/>. Creates any
    /// missing parent directories. Returns <c>true</c> on success,
    /// <c>false</c> on any failure (the implementation logs details).
    /// Must not throw.
    /// </summary>
    /// <param name="lnkPath">Absolute path to the .lnk to create (e.g.
    ///   <c>%APPDATA%\Microsoft\Windows\Start Menu\Programs\Foo\Foo.lnk</c>).
    ///   Parent folder will be created if missing.</param>
    /// <param name="targetExePath">Absolute path to the executable the
    ///   shortcut should launch. Used as both the TargetPath and the
    ///   IconLocation source (with index 0).</param>
    /// <param name="description">Shell tooltip / accessibility
    ///   description for the shortcut.</param>
    bool TryCreateShortcut(string lnkPath, string targetExePath, string description);
}
