using System.Reflection;

namespace ClaudeDesktopLauncher.Tests.RepoHealth;

/// <summary>
/// Repository-health test that enforces the project's hard rule:
/// <b>no .cs or .axaml file in the solution may exceed 200 lines</b>.
/// Exceeding the limit is a SOLID violation, not a style preference -
/// the rule forces extraction via OOP rather than letting files grow
/// into monoliths.
///
/// <para>
/// This test runs as part of <c>dotnet test</c>, so CI naturally fails
/// on any PR that introduces an oversized file. When a file does grow
/// past 200, the fix is <b>extract</b>, not <b>trim</b> - never delete
/// comments, code, or whitespace to hit the limit.
/// </para>
///
/// <para>
/// Scope: every <c>.cs</c> and <c>.axaml</c> file under the solution
/// root, excluding build output (<c>bin/</c>, <c>obj/</c>), test
/// results, the <c>.git/</c> directory, and <c>node_modules/</c>. The
/// solution root is located by walking up from the test binary's
/// directory looking for the <c>.sln</c> file.
/// </para>
/// </summary>
public class FileSizeLimitTests
{
    private const int MaxLines = 200;
    private static readonly string[] ExcludedDirectories =
    {
        "bin", "obj", "TestResults", ".git", "node_modules"
    };
    private static readonly string[] CheckedExtensions = { ".cs", ".axaml" };

    [Fact]
    public void NoCodeFileExceedsTwoHundredLines()
    {
        var solutionRoot = FindSolutionRoot();
        var violators = FindViolators(solutionRoot).ToList();

        if (violators.Count == 0) return;

        var lines = violators
            .OrderByDescending(v => v.LineCount)
            .Select(v => $"  {v.LineCount} lines: {v.RelativePath}");
        var message =
            $"The following files exceed the {MaxLines}-line limit:\n" +
            string.Join("\n", lines) +
            "\n\nHard rule: no .cs or .axaml file may exceed 200 lines. " +
            "Extract via OOP (split into smaller classes / UserControls) - " +
            "never remove comments, code, or whitespace to hit the limit.";
        Assert.Fail(message);
    }

    private record FileViolation(int LineCount, string RelativePath);

    private static IEnumerable<FileViolation> FindViolators(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!CheckedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
            if (IsInExcludedDirectory(file, root)) continue;

            var lineCount = File.ReadAllLines(file).Length;
            if (lineCount > MaxLines)
            {
                var rel = Path.GetRelativePath(root, file);
                yield return new FileViolation(lineCount, rel);
            }
        }
    }

    private static bool IsInExcludedDirectory(string filePath, string root)
    {
        var rel = Path.GetRelativePath(root, filePath);
        var segments = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => ExcludedDirectories.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Walks up from the test binary's directory looking for the .sln
    /// file. Using the test assembly location means this works equally
    /// from local <c>dotnet test</c>, CI runners, and IDE test runs.
    /// </summary>
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
            "Could not find solution root (no .sln file walking up from test assembly)");
    }
}
