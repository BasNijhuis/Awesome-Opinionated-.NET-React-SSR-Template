using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis;
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
    public async Task Passes_On_Union()
    {
        // A C# 15 union compiles to a struct, but it is an immutable closed set of cases (ADR-0020).
        await VerifyEnforcedAsync(
            """
            public sealed record Cat(string Name);
            public sealed record Dog(string Name);
            public union Pet(Cat, Dog);
            """,
            onDotNet11: true
        );
    }

    [Fact]
    public async Task Fires_On_PlainStruct_Next_To_A_Union()
    {
        // The union exemption is keyed on the type being a union, not on structs in general.
        await VerifyEnforcedAsync(
            """
            public sealed record Ok<T>(T Value);
            public sealed record Problem(string Message);
            public union Outcome<T>(Ok<T>, Problem);

            public struct {|ACME002:Point|}
            {
                public int X { get; init; }
            }
            """,
            onDotNet11: true
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

    private static async Task VerifyEnforcedAsync(string source, bool onDotNet11 = false)
    {
        var test = new CSharpAnalyzerTest<DomainTypeKindAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = onDotNet11
                ? new ReferenceAssemblies("net11.0")
                : ReferenceAssemblies.Net.Net80,
        };

        // Unions need the .NET 11 BCL (UnionAttribute / IUnion) and the testing package has no
        // net11.0 preset, so those tests compile against the framework this test host runs on.
        if (onDotNet11)
        {
            test.TestState.AdditionalReferences.AddRange(RunningFramework.Value);
        }

        // Opt the analyzed compilation in to enforcement via a global analyzer config.
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static readonly Lazy<MetadataReference[]> RunningFramework = new(() =>
    {
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetDirectoryName(path) == frameworkDirectory)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    });

    private const string GlobalConfig = """
        is_global = true
        build_property.EnforceImmutability = true
        """;
}
