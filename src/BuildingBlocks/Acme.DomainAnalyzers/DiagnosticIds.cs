namespace Acme.DomainAnalyzers;

/// <summary>Shared identifiers for the domain analyzers' diagnostics.</summary>
/// <remarks>
/// Retired ids are never reused (ADR-0020): <c>{Prefix}007</c> (guarded <c>Result&lt;T&gt;.Value</c>
/// reads) and <c>{Prefix}008</c> (prefer implicit <c>Result&lt;T&gt;</c> conversions) went when
/// <c>Result&lt;T&gt;</c> became a C# union. <c>RetiredDiagnosticIdsTests</c> guards this.
/// </remarks>
internal static class DiagnosticIds
{
    /// <summary>Common prefix for every domain-analyzer diagnostic id (e.g. <c>ACME001</c>).</summary>
    public const string Prefix = "ACME";
}
