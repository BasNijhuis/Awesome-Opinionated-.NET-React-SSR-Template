using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainImmutabilityAnalyzerTests
{
    // Shared boilerplate referenced by the analyzed sources so the markup compiles.
    private const string Preamble = """
        using System.Collections.Generic;
        using System.Collections.Immutable;

        """;

    [Fact]
    public async Task Fires_On_PublicSetter()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public int {|ACME001:X|} { get; set; }
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_PrivateSetter()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public int {|ACME001:X|} { get; private set; }
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_NonReadonlyField()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private int {|ACME001:_x|};
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_ExposedListProperty()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public List<int> {|ACME001:Items|} { get; }
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_PublicListField()
    {
        // A non-private mutable-collection member is exposed mutable state (R5c).
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public readonly List<int> {|ACME001:Items|} = new();
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_PrivateReadonlyListField()
    {
        // A private readonly mutable-collection field is not exposed mutable state: R5c skips
        // private members, R5b passes (readonly), R5a passes (no setter). This is what allows the
        // AggregateRoot domain-events outbox without any exemption attribute.
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private readonly List<int> _items = new();
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_InitOnlyProperty()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public int X { get; init; }
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_ReadonlyField()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private readonly int _x;
                    public Aggregate(int x) => _x = x;
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_ReadOnlyListMember()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public IReadOnlyList<int> Items { get; init; } = ImmutableArray<int>.Empty;
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_RecordWithInitOnlyProps()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed record Aggregate(int X, string Name);
                """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on blatant violations.
        // The {|ACME001:...|} markup is intentionally absent because nothing should fire.
        var test = new CSharpAnalyzerTest<DomainImmutabilityAnalyzer, DefaultVerifier>
        {
            TestCode =
                Preamble
                + """
                    public sealed class Aggregate
                    {
                        public int X { get; set; }
                        private int _y;
                    }
                    """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyEnforcedAsync(string source)
    {
        var test = new CSharpAnalyzerTest<DomainImmutabilityAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // Modern reference set so records / init-only / collection types resolve.
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
