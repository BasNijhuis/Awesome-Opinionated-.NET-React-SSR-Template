using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainPublicInitAnalyzerTests
{
    [Fact]
    public async Task Fires_On_PublicInit()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int {|ACME005:X|} { get; init; }
            }
            """
        );
    }

    [Fact]
    public async Task Fires_On_PublicInit_AmongAllowedMembers()
    {
        // Only the public-init property is flagged; the private init and get-only members are fine.
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int Id { get; private init; }
                public string {|ACME005:Name|} { get; init; }
                public bool Active { get; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_PrivateInit()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int X { get; private init; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_ProtectedInit()
    {
        // Non-sealed so 'protected' is meaningful (no CS0628 on a sealed type).
        await VerifyEnforcedAsync(
            """
            public record Aggregate
            {
                public int X { get; protected init; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_InternalInit()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int X { get; internal init; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_GetOnlyProperty()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int X { get; }

                public Aggregate(int x) => X = x;
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_ExpressionBodiedProperty()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public int X => 42;
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_ReadonlyField()
    {
        await VerifyEnforcedAsync(
            """
            public sealed record Aggregate
            {
                public readonly int X;

                public Aggregate(int x) => X = x;
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_PositionalRecord()
    {
        // A positional record's primary-constructor parameters generate a *public* init accessor, but it
        // is compiler-generated (IsImplicitlyDeclared) and therefore exempt — the ctor is the only path.
        await VerifyEnforcedAsync(
            """
            public sealed record Vo(int X, string Name);
            """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on a public init.
        // The {|ACME005:...|} markup is intentionally absent because nothing should fire.
        var test = new CSharpAnalyzerTest<DomainPublicInitAnalyzer, DefaultVerifier>
        {
            TestCode = """
                public sealed record Aggregate
                {
                    public int X { get; init; }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyEnforcedAsync(string source)
    {
        var test = new CSharpAnalyzerTest<DomainPublicInitAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // Modern reference set so records / init-only resolve.
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        // Opt the analyzed compilation in to enforcement via a global analyzer config.
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private const string GlobalConfig = """
        is_global = true
        build_property.EnforceImmutability = true
        """;
}
