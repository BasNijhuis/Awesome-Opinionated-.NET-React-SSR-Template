using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainSelfTypeArgumentAnalyzerTests
{
    // A stub mirroring AggregateRoot.RaiseEvent<TSelf>: the self-type method whose argument must
    // be the calling type. Included in every test source so call sites resolve.
    private const string SelfTypeBase = """
        public abstract record AggregateRoot
        {
            protected TSelf RaiseEvent<TSelf>()
                where TSelf : AggregateRoot => (TSelf)this;
        }
        """;

    [Fact]
    public async Task Fires_When_SelfTypeArgument_IsNotCallingType()
    {
        // Foo calls RaiseEvent<Bar>() — Bar != Foo, so the runtime cast would throw.
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public sealed record Bar : AggregateRoot;

                public sealed record Foo : AggregateRoot
                {
                    // Returns the base type so the call site compiles; Bar != Foo still trips ACME004.
                    public AggregateRoot Next() => {|ACME004:RaiseEvent<Bar>()|};
                }
                """
        );
    }

    [Fact]
    public async Task Passes_When_SelfTypeArgument_IsCallingType()
    {
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public sealed record Foo : AggregateRoot
                {
                    public Foo Next() => RaiseEvent<Foo>();
                }
                """
        );
    }

    [Fact]
    public async Task Passes_On_GenericMethod_WithoutSelfTypeParameter()
    {
        // A generic method whose type parameter is not named TSelf is ignored entirely.
        await VerifyEnforcedAsync(
            """
            public static class Helpers
            {
                public static T Identity<T>(T value) => value;
            }

            public sealed record Foo
            {
                public int Use() => Helpers.Identity<int>(1);
            }
            """
        );
    }

    [Fact]
    public async Task Passes_When_SelfTypeArgument_IsForwardedTypeParameter()
    {
        // A generic method that forwards its own type parameter as TSelf must not false-positive:
        // the supplied argument is a type parameter, not a concrete type, so it cannot be compared
        // to a concrete enclosing type.
        await VerifyEnforcedAsync(
            SelfTypeBase
                + """

                public abstract record Forwarding : AggregateRoot
                {
                    protected TSelf RaiseFor<TSelf>()
                        where TSelf : Forwarding => RaiseEvent<TSelf>();
                }
                """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on a clear mismatch.
        var test = new CSharpAnalyzerTest<DomainSelfTypeArgumentAnalyzer, DefaultVerifier>
        {
            TestCode =
                SelfTypeBase
                + """

                    public sealed record Bar : AggregateRoot;

                    public sealed record Foo : AggregateRoot
                    {
                        public AggregateRoot Next() => RaiseEvent<Bar>();
                    }
                    """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyEnforcedAsync(string source)
    {
        var test = new CSharpAnalyzerTest<DomainSelfTypeArgumentAnalyzer, DefaultVerifier>
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
