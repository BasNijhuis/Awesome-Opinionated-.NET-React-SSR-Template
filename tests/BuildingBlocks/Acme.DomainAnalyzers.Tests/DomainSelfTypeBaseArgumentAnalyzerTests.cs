using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainSelfTypeBaseArgumentAnalyzerTests
{
    // A stub mirroring AggregateRoot<TSelf, TId>: the self-typed base whose TSelf argument must be the
    // declaring type. Included in every test source so base lists resolve.
    private const string SelfTypeBase = """
        public abstract record AggregateRoot;

        public abstract record AggregateRoot<TSelf, TId> : AggregateRoot
            where TSelf : AggregateRoot<TSelf, TId>
        {
            protected TSelf RaiseEvent() => (TSelf)this;
        }
        """;

    [Fact]
    public async Task Fires_When_SelfTypeArgument_IsNotDeclaringType()
    {
        // Gadget passes Widget as TSelf — the constraint is satisfied, but RaiseEvent's cast would throw.
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public sealed record Widget : AggregateRoot<Widget, int>;

                public sealed record {|ACME009:Gadget|} : AggregateRoot<Widget, int>;
                """
        );
    }

    [Fact]
    public async Task Passes_When_SelfTypeArgument_IsDeclaringType()
    {
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public sealed record Widget : AggregateRoot<Widget, int>;
                """
        );
    }

    [Fact]
    public async Task Fires_When_SelfTypedBase_IsInheritedThroughAnIntermediate()
    {
        // The intermediate closes TSelf on itself, which is fine for the intermediate — but every leaf
        // under it inherits TSelf = Audited, so two leaves sharing an id would compare as the same
        // aggregate (and RaiseEvent would return Audited). The intermediate must forward TSelf instead.
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public abstract record Audited : AggregateRoot<Audited, int>;

                public sealed record {|ACME009:Widget|} : Audited;
                """
        );
    }

    [Fact]
    public async Task Passes_When_SelfTypeArgument_IsForwardedTypeParameter()
    {
        // An abstract middle layer forwards its own TSelf to the base; there is no concrete declaring
        // type to compare against, and the leaf that closes it is checked on its own declaration.
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public abstract record Middle<TSelf> : AggregateRoot<TSelf, int>
                    where TSelf : Middle<TSelf>;

                public sealed record Widget : Middle<Widget>;
                """
        );
    }

    [Fact]
    public async Task Fires_When_SelfTypedInterface_IsNotDeclaringType()
    {
        // The convention is the parameter name, not the base kind: an interface counts too.
        await VerifyEnforcedAsync(
            """
            public interface ISelfTyped<TSelf>
                where TSelf : ISelfTyped<TSelf>;

            public sealed record Widget : ISelfTyped<Widget>;

            public sealed record {|ACME009:Gadget|} : ISelfTyped<Widget>;
            """
        );
    }

    [Fact]
    public async Task Passes_On_GenericBase_WithoutSelfTypeParameter()
    {
        // A generic base whose type parameter is not named TSelf is ignored entirely.
        await VerifyEnforcedAsync(
            """
            public abstract record Box<T>
            {
                public T? Value { get; init; }
            }

            public sealed record Widget : Box<int>;
            """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on a clear mismatch.
        var test = new CSharpAnalyzerTest<DomainSelfTypeBaseArgumentAnalyzer, DefaultVerifier>
        {
            TestCode =
                SelfTypeBase
                + """

                    public sealed record Widget : AggregateRoot<Widget, int>;

                    public sealed record Gadget : AggregateRoot<Widget, int>;
                    """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyEnforcedAsync(string source)
    {
        var test = new CSharpAnalyzerTest<DomainSelfTypeBaseArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
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
