using System.Xml.Linq;

namespace Acme.Architecture.Tests;

/// <summary>Solution-wide build settings that the architecture relies on.</summary>
public sealed class BuildSettingsTests
{
    [Theory]
    [InlineData("CS8509")] // switch expression misses a case
    [InlineData("CS8846")] // ... or covers it only behind a `when` guard
    public void A_non_exhaustive_switch_expression_is_a_build_error(string warning)
    {
        // Arrange: a Result<T> consumer that forgets the failure case must not compile (ADR-0020).
        var props = XDocument.Load(Path.Combine(RepoRoot(), "Directory.Build.props"));

        // Act
        var warningsAsErrors = props
            .Descendants("WarningsAsErrors")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.TrimEntries));

        // Assert
        warningsAsErrors.Should().Contain(warning);
    }

    [Fact]
    public void No_project_suppresses_or_drops_the_exhaustiveness_errors()
    {
        // Arrange: every MSBuild file in the repo, so a single project can't opt out quietly.
        var root = RepoRoot();
        var buildFiles = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path =>
                path.EndsWith(".csproj") || path.EndsWith(".props") || path.EndsWith(".targets")
            )
            .Where(path => !IsUnder(path, "bin", "obj", "node_modules", ".git", ".claude"))
            .Select(path => (Path: path, Xml: XDocument.Load(Path.Combine(root, path))));

        // Act
        var offenders = buildFiles
            .SelectMany(file =>
                file.Xml.Descendants("NoWarn")
                    .Where(element =>
                        element.Value.Contains("CS8509") || element.Value.Contains("CS8846")
                    )
                    .Select(_ => $"{file.Path}: NoWarn suppresses an exhaustiveness error")
                    .Concat(
                        file.Xml.Descendants("WarningsAsErrors")
                            .Where(element =>
                                !element.Value.TrimStart().StartsWith("$(WarningsAsErrors)")
                            )
                            .Select(_ => $"{file.Path}: WarningsAsErrors drops the inherited list")
                    )
            )
            .ToList();

        // Assert
        offenders.Should().BeEmpty();
    }

    // Relative to the repo root, so a checkout that itself lives under e.g. .claude/ isn't excluded.
    private static bool IsUnder(string relativePath, params string[] directoryNames) =>
        relativePath.Split(Path.DirectorySeparatorChar).Any(directoryNames.Contains);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))
        )
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "No Directory.Build.props above the test output."
            );
    }
}
