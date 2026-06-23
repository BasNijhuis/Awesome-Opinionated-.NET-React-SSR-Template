using Acme.DomainAnalyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Acme.DomainAnalyzers.Tests;

public sealed class EfEntityMustNotBeDomainTypeAnalyzerTests
{
    [Fact]
    public async Task Flags_DbSet_of_domain_type()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Ctx : DbContext
            {
                public DbSet<Aggregate> {|ACME006:Bad|} { get; set; }
            }
            """
        );
    }

    [Fact]
    public async Task Flags_Entity_generic()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Cfg
            {
                public void M(ModelBuilder mb) => {|ACME006:mb.Entity<Aggregate>()|};
            }
            """
        );
    }

    [Fact]
    public async Task Flags_Entity_typeof_overload()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Cfg
            {
                public void M(ModelBuilder mb) => mb.Entity({|ACME006:typeof(Aggregate)|});
            }
            """
        );
    }

    [Fact]
    public async Task Flags_Set_of_domain_type()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Ctx : DbContext
            {
                public object Get() => {|ACME006:Set<Aggregate>()|};
            }
            """
        );
    }

    [Fact]
    public async Task Flags_HasOne_HasMany_OwnsOne()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Cfg
            {
                public void M(EntityTypeBuilder<FakeEntity> b)
                {
                    {|ACME006:b.HasOne<Aggregate>()|};
                    {|ACME006:b.HasMany<Aggregate>()|};
                    {|ACME006:b.OwnsOne<SessionId>()|};
                }
            }

            public sealed class FakeEntity { }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_application_entity()
    {
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Application;

            public sealed class Ctx : DbContext
            {
                public DbSet<FooEntity> Good { get; set; }
                public void M(ModelBuilder mb) => mb.Entity<FooEntity>();
            }
            """
        );
    }

    [Fact]
    public async Task Does_not_flag_value_object_property_conversion()
    {
        // The sanctioned value-object mapping: Property(...).HasConversion(...). Neither method is an
        // entity registration, so the domain value object referenced there is not flagged.
        await VerifyAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Application;

            public sealed class Cfg
            {
                public void M(EntityTypeBuilder<FooEntity> b) => b.Property(e => e.Id).HasConversion<int>();
            }
            """
        );
    }

    [Fact]
    public async Task Passes_when_gate_off()
    {
        var test = NewTest(
            """
            using Microsoft.EntityFrameworkCore;
            using Acme.Domain;

            public sealed class Ctx : DbContext
            {
                public DbSet<Aggregate> Bad { get; set; }
            }
            """
        );
        // No .globalconfig -> gate off -> no diagnostics even on a domain DbSet.
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static CSharpAnalyzerTest<EfEntityMustNotBeDomainTypeAnalyzer, DefaultVerifier> NewTest(
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
        build_property.EnforceEfEntityBoundary = true
        """;

    // Minimal domain + EF stubs so the analyzer's namespace/name checks resolve without real packages.
    private const string Stubs = """
        namespace Acme.Domain
        {
            public sealed record Aggregate;
            public readonly record struct SessionId(System.Guid Value);
        }

        namespace Acme.Application
        {
            public sealed class FooEntity
            {
                public Acme.Domain.SessionId Id { get; set; }
            }
        }

        namespace Microsoft.EntityFrameworkCore
        {
            using System;
            using System.Linq.Expressions;

            public class DbSet<T> { }

            public class PropertyBuilder<P>
            {
                public PropertyBuilder<P> HasConversion<C>() => this;
            }

            public class EntityTypeBuilder<T>
            {
                public object HasOne<U>() => null!;
                public object HasMany<U>() => null!;
                public object OwnsOne<U>() => null!;
                public PropertyBuilder<P> Property<P>(Expression<Func<T, P>> e) => null!;
            }

            public class ModelBuilder
            {
                public EntityTypeBuilder<T> Entity<T>() => null!;
                public object Entity(Type t) => null!;
            }

            public class DbContext
            {
                public DbSet<T> Set<T>() => null!;
            }
        }
        """;
}
