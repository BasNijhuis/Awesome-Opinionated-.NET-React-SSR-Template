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
