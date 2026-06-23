using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class DomainTypeKindAnalyzerTests
{
    [Fact]
    public async Task Fires_On_PlainClass()
    {
        await VerifyEnforcedAsync(
            """
            public sealed class {|ACME002:Aggregate|}
            {
                public int X { get; init; }
            }
            """
        );
    }

    [Fact]
    public async Task Fires_On_PlainStruct()
    {
        await VerifyEnforcedAsync(
            """
            public struct {|ACME002:Point|}
            {
                public int X { get; init; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_Record()
    {
        await VerifyEnforcedAsync("public sealed record Aggregate(int X, string Name);");
    }

    [Fact]
    public async Task Passes_On_RecordStruct()
    {
        await VerifyEnforcedAsync("public readonly record struct Point(int X, int Y);");
    }

    [Fact]
    public async Task Passes_On_Interface()
    {
        await VerifyEnforcedAsync(
            """
            public interface IThing
            {
                int X { get; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_Enum()
    {
        await VerifyEnforcedAsync(
            """
            public enum Suit
            {
                Hearts,
                Spades,
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_StaticClass()
    {
        // A static class is a utility holder and cannot be a record — exempt.
        await VerifyEnforcedAsync(
            """
            public static class Helpers
            {
                public static int Double(int x) => x * 2;
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_ClassDerivingFromNonRecordBase()
    {
        // A class deriving from a non-record base (here System.Exception) must stay a class — exempt.
        await VerifyEnforcedAsync(
            """
            public sealed class DomainException : System.Exception
            {
                public DomainException(string message)
                    : base(message) { }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_On_ClassDerivingFromNonRecordBaseClass()
    {
        // The exempt base itself is a plain class, so it (the base) is still flagged; the derived
        // class is exempt because it extends a non-record base.
        await VerifyEnforcedAsync(
            """
            public abstract class {|ACME002:BaseThing|}
            {
                public int X { get; init; }
            }

            public sealed class Derived : BaseThing
            {
                public int Y { get; init; }
            }
            """
        );
    }

    [Fact]
    public async Task Passes_When_GateOff()
    {
        // No enforcement property -> analyzer reports nothing even on a plain class.
        var test = new CSharpAnalyzerTest<DomainTypeKindAnalyzer, DefaultVerifier>
        {
            TestCode = """
                public sealed class Aggregate
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
        var test = new CSharpAnalyzerTest<DomainTypeKindAnalyzer, DefaultVerifier>
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
