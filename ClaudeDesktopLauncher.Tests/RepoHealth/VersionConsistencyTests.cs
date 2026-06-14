using System.Reflection;
using System.Xml.Linq;

namespace ClaudeDesktopLauncher.Tests.RepoHealth;

/// <summary>
/// Repository-health test that asserts the version displayed in the
/// launcher footer matches the <c>&lt;Version&gt;</c> element in the
/// main project's <c>.csproj</c> file.
///
/// <para>
/// The launcher reads its version from
/// <c>Assembly.GetEntryAssembly().GetName().Version</c> which maps to
/// <c>AssemblyVersion</c>, not <c>&lt;Version&gt;</c>. Previously
/// these diverged silently (v1.10.5 footer on a v1.10.7 install)
/// because the explicit <c>&lt;AssemblyVersion&gt;</c> element in the
/// csproj was never bumped alongside <c>&lt;Version&gt;</c>. The fix
/// was to remove the explicit <c>&lt;AssemblyVersion&gt;</c> so MSBuild
/// derives it from <c>&lt;Version&gt;</c> automatically. This test
/// ensures the two can never diverge again.
/// </para>
///
/// <para>
/// How it works: reads the compiled <c>AssemblyVersion</c> from the
/// test assembly's sibling app DLL (same output directory), then reads
/// the <c>&lt;Version&gt;</c> element from the csproj found by walking
/// up from the solution root. Asserts they share the same
/// Major.Minor.Build prefix — the revision component (fourth segment)
/// is intentionally ignored because MSBuild defaults it to 0 while
/// NuGet/Velopack versions only use three segments.
/// </para>
/// </summary>
public class VersionConsistencyTests
{
    private const string AppDllName = "ClaudeDesktopLauncher.dll";
    private const string CsprojName = "ClaudeDesktopLauncher.csproj";

    [Fact]
    public void FooterVersionMatchesCsprojVersion()
    {
        var assemblyVersion = GetCompiledAssemblyVersion();
        var csprojVersion   = GetCsprojVersion();

        // Compare Major.Minor.Build — ignore the Revision component.
        var assemblyPrefix = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        var csprojPrefix   = string.Join(".", csprojVersion.Split('.').Take(3));

        Assert.True(
            assemblyPrefix == csprojPrefix,
            $"Footer version mismatch detected.\n" +
            $"  Compiled AssemblyVersion: {assemblyVersion} → footer shows v{assemblyPrefix}\n" +
            $"  Csproj <Version>:         {csprojVersion}\n\n" +
            $"The launcher footer reads AssemblyVersion (from the compiled DLL), " +
            $"not the NuGet <Version> tag. MSBuild derives AssemblyVersion from " +
            $"<Version> automatically when no explicit <AssemblyVersion> element " +
            $"is present — do not add one. If they diverge, remove the explicit " +
            $"<AssemblyVersion> element from the .csproj and let MSBuild sync it.");
    }

    private static Version GetCompiledAssemblyVersion()
    {
        var dir    = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var appDll = Path.Combine(dir, AppDllName);

        if (!File.Exists(appDll))
            throw new FileNotFoundException(
                $"Could not find {AppDllName} next to the test assembly at {dir}. " +
                $"Ensure the main project is built before running tests.");

        var name = AssemblyName.GetAssemblyName(appDll);
        return name.Version
            ?? throw new InvalidOperationException(
                $"AssemblyName.Version is null for {appDll}.");
    }

    private static string GetCsprojVersion()
    {
        var solutionRoot = FindSolutionRoot();
        var csproj = Directory
            .EnumerateFiles(solutionRoot, CsprojName, SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            ?? throw new FileNotFoundException(
                $"Could not find {CsprojName} under {solutionRoot}.");

        var doc     = XDocument.Load(csproj);
        var version = doc.Descendants("Version").FirstOrDefault()?.Value;

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException(
                $"No <Version> element found in {csproj}.");

        return version;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not find solution root (no .sln file walking up from test assembly).");
    }
}
