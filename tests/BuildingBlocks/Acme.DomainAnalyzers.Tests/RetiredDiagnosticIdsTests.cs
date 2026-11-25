using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acme.DomainAnalyzers.Tests;

/// <summary>
/// Retired diagnostic ids are never reused (ADR-0020): a reused id would make old suppressions and
/// docs silently apply to a different rule.
/// </summary>
public sealed class RetiredDiagnosticIdsTests
{
    // 007 (Result<T>.Value guard) and 008 (prefer implicit Result conversions) went with the move of
    // Result<T> to a C# union. Suffixes only: the prefix is derived so `setup.cs` renames stay valid.
    private static readonly string[] RetiredSuffixes = ["007", "008"];

    [Fact]
    public void No_analyzer_reports_a_retired_id()
    {
        // Arrange
        var retired = RetiredSuffixes.Select(suffix => Prefix + suffix).ToArray();

        // Act
        var ids = SupportedDiagnosticIds();

        // Assert
        ids.Should().NotIntersectWith(retired);
    }

    [Fact]
    public void Every_diagnostic_id_belongs_to_one_analyzer()
    {
        // Act
        var ids = SupportedDiagnosticIds();

        // Assert
        ids.Should().OnlyHaveUniqueItems();
    }

    // e.g. "ACME" from "ACME002".
    private static string Prefix => DomainTypeKindAnalyzer.DiagnosticId[..^3];

    private static List<string> SupportedDiagnosticIds() =>
        typeof(DomainTypeKindAnalyzer)
            .Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DiagnosticAnalyzer)) && !type.IsAbstract)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Select(descriptor => descriptor.Id)
            .ToList();
}
