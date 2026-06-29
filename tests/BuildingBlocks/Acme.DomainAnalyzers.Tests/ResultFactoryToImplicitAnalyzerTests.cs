using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class ResultFactoryToImplicitAnalyzerTests
{
    [Fact]
    public async Task Flags_success_in_return()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M() => {|ACME008:Result.Success(1)|};
            }
            """
        );
    }

    [Fact]
    public async Task Flags_generic_success_in_return()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M()
                {
                    return {|ACME008:Result<int>.Success(1)|};
                }
            }
            """
        );
    }

    [Fact]
    public async Task Flags_failure_with_single_error_in_return()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M(Error e) => {|ACME008:Result<int>.Failure(e)|};
            }
            """
        );
    }

    [Fact]
    public async Task Flags_in_ternary_branches()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M(bool ok, Error e) =>
                    ok ? {|ACME008:Result.Success(1)|} : {|ACME008:Result<int>.Failure(e)|};
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_failure_with_error_list()
    {
        // No implicit conversion from IReadOnlyList<Error> (interface, CS0552) — must stay explicit.
        await VerifyAsync(
            """
            using System.Collections.Generic;
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M(IReadOnlyList<Error> errors) => Result<int>.Failure(errors);
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_when_not_returned()
    {
        // Argument position: generic inference on the outer call would bind the wrong type, so the
        // factory call must stay.
        await VerifyAsync(
            """
            using System.Threading.Tasks;
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Task<Result<int>> M() => Task.FromResult(Result.Success(1));
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_return_target_of_non_generic_result()
    {
        // The non-generic Result has no implicit value conversion, so returning a Result<int> through
        // it cannot be simplified.
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result M() => Result<int>.Success(1);
            }
            """
        );
    }

    [Fact]
    public async Task Passes_when_gate_off()
    {
        var test = NewTest(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static Result<int> M() => Result.Success(1);
            }
            """
        );
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static CSharpAnalyzerTest<ResultFactoryToImplicitAnalyzer, DefaultVerifier> NewTest(
        string source
    ) =>
        new()
        {
            TestState = { Sources = { Stubs, source } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

    private static async Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = NewTest(source);
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private const string GlobalConfig = """
        is_global = true
        build_property.EnforceResultImplicitConversion = true
        """;

    // Minimal Result/Error stubs in a *.DomainAbstractions namespace, with the same factory shapes and
    // implicit conversions as the real building block.
    private const string Stubs = """
        using System.Collections.Generic;
        using System.Collections.Immutable;

        namespace Acme.DomainAbstractions
        {
            public sealed record Error
            {
                public static Error Of(string code) => new();
            }

            public record Result
            {
                public static Result<T> Success<T>(T value) => Result<T>.Success(value);
                public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
            }

            public sealed record Result<T> : Result
            {
                public static Result<T> Success(T value) => new();
                public static new Result<T> Failure(Error error) => new();
                public static new Result<T> Failure(IReadOnlyList<Error> errors) => new();

                public static implicit operator Result<T>(T value) => Success(value);
                public static implicit operator Result<T>(Error error) => Failure(error);
                public static implicit operator Result<T>(ImmutableArray<Error> errors) => Failure(errors);
            }
        }
        """;
}
