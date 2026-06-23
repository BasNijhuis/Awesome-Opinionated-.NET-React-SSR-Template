using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainMutableEnumerableAnalyzerTests
{
    // Shared boilerplate referenced by the analyzed sources so the markup compiles.
    private const string Preamble = """
        using System.Collections.Generic;
        using System.Collections.Immutable;

        """;

    [Fact]
    public async Task Fires_On_ListField()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private readonly List<int> {|ACME003:_items|} = new();
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_DictionaryProperty()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public Dictionary<string, int> {|ACME003:Map|} { get; init; }
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_ArrayField()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private readonly int[] {|ACME003:_values|} = new int[0];
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_MethodParameter()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public void Add(ICollection<int> {|ACME003:items|}) { }
                }
                """
        );
    }

    [Fact]
    public async Task Fires_On_MethodReturningList()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public List<int> {|ACME003:All|}() => new();
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_ReadOnlyListProperty()
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
    public async Task Passes_On_ReadOnlyDictionaryProperty()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public IReadOnlyDictionary<string, int> Map { get; init; }
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_EnumerableParameterAndReturn()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    public IEnumerable<int> Transform(IEnumerable<int> items) => items;
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_ImmutableArrayField()
    {
        await VerifyEnforcedAsync(
            Preamble
                + """
                public sealed class Aggregate
                {
                    private static readonly ImmutableArray<int> Values = ImmutableArray<int>.Empty;
                }
                """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on a blatant List<> member.
        var test = new CSharpAnalyzerTest<DomainMutableEnumerableAnalyzer, DefaultVerifier>
        {
            TestCode =
                Preamble
                + """
                    public sealed class Aggregate
                    {
                        private readonly List<int> _items = new();
                        public int[] Values { get; init; }
                    }
                    """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyEnforcedAsync(string source)
    {
        var test = new CSharpAnalyzerTest<DomainMutableEnumerableAnalyzer, DefaultVerifier>
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
