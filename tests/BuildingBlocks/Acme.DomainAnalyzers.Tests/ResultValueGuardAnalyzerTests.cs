using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class ResultValueGuardAnalyzerTests
{
    [Fact]
    public async Task Flags_value_read_off_a_temporary()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M() => {|ACME007:Sample.Make().Value|};
            }
            """
        );
    }

    [Fact]
    public async Task Flags_unguarded_local()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    return {|ACME007:r.Value|};
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_early_return_on_failure()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    if (r.IsFailure)
                    {
                        return 0;
                    }

                    return r.Value;
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_early_throw_on_negated_success()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    if (!r.IsSuccess)
                    {
                        throw new System.InvalidOperationException();
                    }

                    return r.Value;
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_inside_if_success_block()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    if (r.IsSuccess)
                    {
                        return r.Value;
                    }

                    return 0;
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_ternary_success_branch()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    return r.IsSuccess ? r.Value : 0;
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_else_branch_of_failure_check()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    if (r.IsFailure)
                    {
                        return 0;
                    }
                    else
                    {
                        return r.Value;
                    }
                }
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_non_result_value()
    {
        // Nullable<T>.Value (and Lazy<T>.Value, strongly-typed-id .Value, …) are not Result<T>.Value.
        await VerifyAsync(
            """
            public static class C
            {
                public static int M(int? n) => n.Value;
            }
            """
        );
    }

    [Fact]
    public async Task Flags_when_guard_is_on_a_different_result()
    {
        await VerifyAsync(
            """
            using Acme.DomainAbstractions;

            public static class C
            {
                public static int M()
                {
                    var r = Sample.Make();
                    var other = Sample.Make();
                    if (other.IsSuccess)
                    {
                        return {|ACME007:r.Value|};
                    }

                    return 0;
                }
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
                public static int M()
                {
                    var r = Sample.Make();
                    return r.Value;
                }
            }
            """
        );
        // No .globalconfig -> gate off -> no diagnostics even on an unguarded Value read.
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static CSharpAnalyzerTest<ResultValueGuardAnalyzer, DefaultVerifier> NewTest(
        string source
    ) =>
        new()
        {
            TestState = { Sources = { Stubs, source } },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

    private static async Task VerifyAsync(string source)
    {
        var test = NewTest(source);
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", GlobalConfig));
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private const string GlobalConfig = """
        is_global = true
        build_property.EnforceResultValueGuard = true
        """;

    // Minimal Result<T> stub in a *.DomainAbstractions namespace so the analyzer's type check resolves
    // without the real building block.
    private const string Stubs = """
        namespace Acme.DomainAbstractions
        {
            public sealed class Result<T>
            {
                public bool IsSuccess { get; }
                public bool IsFailure { get; }
                public T Value { get; }
            }

            public static class Sample
            {
                public static Result<int> Make() => null!;
            }
        }
        """;
}
