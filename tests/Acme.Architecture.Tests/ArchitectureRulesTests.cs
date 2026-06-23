using System.Reflection;
using NetArchTest.Rules;

namespace Acme.Architecture.Tests;

/// <summary>
/// Enforces the layering and module-boundary rules so violations fail the build rather than relying
/// on convention. See docs/adr/0014-modular-monolith.md and docs/adr/0018-contract-layering.md.
///
/// Leaf/lower layers use the explicit allow-list form (<c>OnlyHaveDependenciesOn</c>); the broader
/// kernel/host projects use the deny-list form (<c>NotHaveDependencyOnAny</c>).
/// </summary>
public sealed class ArchitectureRulesTests
{
    private const string Modules = "Acme.Modules";

    private static readonly Assembly DomainAbstractions =
        typeof(DomainAbstractions.AssemblyMarker).Assembly;
    private static readonly Assembly Domain = typeof(Kernel.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly CqrsAbstractions =
        typeof(CQRS.Abstractions.AssemblyMarker).Assembly;
    private static readonly Assembly Application =
        typeof(Kernel.Application.AssemblyMarker).Assembly;
    private static readonly Assembly Infrastructure =
        typeof(Kernel.Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly GreetingsDomain =
        typeof(Modules.Greetings.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly GreetingsApplication =
        typeof(Modules.Greetings.Application.AssemblyMarker).Assembly;
    private static readonly Assembly GreetingsContracts =
        typeof(Modules.Greetings.Application.Contracts.AssemblyMarker).Assembly;
    private static readonly Assembly WidgetsContracts =
        typeof(Modules.Widgets.Application.Contracts.AssemblyMarker).Assembly;

    private static readonly Assembly[] ApplicationAssemblies =
    [
        Application,
        typeof(Modules.Greetings.Application.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Application.AssemblyMarker).Assembly,
    ];

    // ---- Shared kernel: explicit allow-lists for the leaf projects ----

    [Fact]
    public void DomainAbstractions_is_a_leaf()
    {
        var result = Types
            .InAssembly(DomainAbstractions)
            .That()
            .ResideInNamespaceStartingWith("Acme")
            .Should()
            .OnlyHaveDependenciesOn("System", "Acme.DomainAbstractions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    [Fact]
    public void Domain_depends_only_on_itself_and_DomainAbstractions()
    {
        var result = Types
            .InAssembly(Domain)
            .That()
            .ResideInNamespaceStartingWith("Acme")
            .Should()
            .OnlyHaveDependenciesOn("System", "Acme.Kernel.Domain", "Acme.DomainAbstractions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    [Fact]
    public void CqrsAbstractions_depends_only_on_DomainAbstractions()
    {
        var result = Types
            .InAssembly(CqrsAbstractions)
            .That()
            .ResideInNamespaceStartingWith("Acme")
            .Should()
            .OnlyHaveDependenciesOn("System", "Acme.CQRS.Abstractions", "Acme.DomainAbstractions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    // ---- Kernel must never depend on a feature module ----

    [Fact]
    public void Kernel_does_not_depend_on_any_module()
    {
        foreach (
            var kernel in new[]
            {
                DomainAbstractions,
                Domain,
                CqrsAbstractions,
                Application,
                Infrastructure,
            }
        )
        {
            var result = Types.InAssembly(kernel).Should().NotHaveDependencyOn(Modules).GetResult();

            result.IsSuccessful.Should().BeTrue($"{kernel.GetName().Name}: {Failures(result)}");
        }
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types
            .InAssembly(Application)
            .Should()
            .NotHaveDependencyOnAny("Acme.Kernel.Infrastructure", "Acme.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    [Fact]
    public void Application_layers_do_not_depend_on_EntityFrameworkCore()
    {
        // Reads are composed behind reader ports; the EF Core projections live in Infrastructure. No
        // Application project may reference EF.
        foreach (var assembly in ApplicationAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name}: {Failures(result)}");
        }
    }

    [Fact]
    public void Domain_event_handlers_do_not_depend_on_the_unit_of_work()
    {
        // Domain-event handlers run in-process inside the unit of work's transaction, before commit.
        // Depending on IUnitOfWork would let a handler commit mid-transaction — forbidden. Cross-aggregate
        // writes are staged by the originating handler.
        var offenders = ApplicationAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsDomainEventHandler)
            .Where(DependsOnUnitOfWork)
            .Select(type => type.Name)
            .ToArray();

        offenders
            .Should()
            .BeEmpty(
                $"domain-event handlers must not inject {nameof(Kernel.Application.Common.Interfaces.IUnitOfWork)}: "
                    + string.Join(", ", offenders)
            );
    }

    [Fact]
    public void Unit_of_work_dependency_detection_has_teeth()
    {
        // Proves the IUnitOfWork-dependency check actually detects an injected unit of work, so the
        // rule above is not vacuously green.
        DependsOnUnitOfWork(typeof(UnitOfWorkConsumer))
            .Should()
            .BeTrue(
                $"the detector must catch an injected {nameof(Kernel.Application.Common.Interfaces.IUnitOfWork)}"
            );
    }

    [Fact]
    public void Query_handlers_do_not_depend_on_repositories()
    {
        // Reads go through the reader ports, never the write-side repositories. A query handler
        // injecting a repository is mixing the read and write models.
        var offenders = ApplicationAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsQueryHandler)
            .Where(DependsOnRepository)
            .Select(type => type.Name)
            .ToArray();

        offenders
            .Should()
            .BeEmpty(
                "query handlers must read via a reader port, not a repository: "
                    + string.Join(", ", offenders)
            );
    }

    [Fact]
    public void Repository_dependency_detection_has_teeth()
    {
        // Proves the repository-dependency check detects an injected repository, so the rule above is
        // not vacuously green.
        DependsOnRepository(typeof(RepositoryConsumer))
            .Should()
            .BeTrue("the detector must catch an injected repository");
    }

    // Local fixtures with the dependency under test, so the "has teeth" checks don't reference
    // production internals by string name.
    private sealed class UnitOfWorkConsumer(
        Kernel.Application.Common.Interfaces.IUnitOfWork unitOfWork
    )
    {
        public Kernel.Application.Common.Interfaces.IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    private sealed class RepositoryConsumer(
        Modules.Greetings.Application.IGreetingRepository repository
    )
    {
        public Modules.Greetings.Application.IGreetingRepository Repository { get; } = repository;
    }

    private static bool IsDomainEventHandler(Type type) =>
        type is { IsAbstract: false, IsInterface: false }
        && type.GetInterfaces()
            .Any(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(CQRS.Abstractions.IDomainEventHandler<>)
            );

    private static bool IsQueryHandler(Type type) =>
        type is { IsAbstract: false, IsInterface: false }
        && type.GetInterfaces()
            .Any(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(CQRS.Abstractions.IQueryHandler<,>)
            );

    private static bool DependsOnUnitOfWork(Type type) =>
        type.GetConstructors()
            .Any(constructor =>
                constructor
                    .GetParameters()
                    .Any(parameter =>
                        parameter.ParameterType
                        == typeof(Kernel.Application.Common.Interfaces.IUnitOfWork)
                    )
            );

    private static bool DependsOnRepository(Type type) =>
        type.GetConstructors()
            .Any(constructor =>
                constructor
                    .GetParameters()
                    .Any(parameter =>
                        parameter.ParameterType.Name.EndsWith(
                            "Repository",
                            StringComparison.Ordinal
                        )
                    )
            );

    // ---- Module internal layering (Domain <- Application <- Infrastructure) ----

    [Fact]
    public void GreetingsDomain_is_a_leaf()
    {
        var result = Types
            .InAssembly(GreetingsDomain)
            .That()
            .ResideInNamespaceStartingWith("Acme")
            .Should()
            .OnlyHaveDependenciesOn(
                "System",
                "Acme.DomainAbstractions",
                "Acme.Modules.Greetings.Domain"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    [Fact]
    public void GreetingsDomain_does_not_depend_on_its_Application_or_Infrastructure()
    {
        var result = Types
            .InAssembly(GreetingsDomain)
            .Should()
            .NotHaveDependencyOnAny(
                "Acme.Modules.Greetings.Application",
                "Acme.Modules.Greetings.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    [Fact]
    public void GreetingsApplication_does_not_depend_on_its_Infrastructure()
    {
        var result = Types
            .InAssembly(GreetingsApplication)
            .Should()
            .NotHaveDependencyOn("Acme.Modules.Greetings.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Failures(result));
    }

    // ---- Module isolation: a module is reachable only through its .Contracts ----

    // Checked at the assembly-reference level because the contract layer's own namespace
    // (Acme.Modules.X.Application.Contracts) is a prefix-child of the forbidden
    // Acme.Modules.X.Application (handlers), so namespace-based rules can't separate them.
    private static IReadOnlyList<string> ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    [Fact]
    public void Contracts_do_not_depend_on_the_modules_internal_layers()
    {
        var forbidden = new[]
        {
            "Acme.Modules.Greetings.Domain",
            "Acme.Modules.Greetings.Application",
            "Acme.Modules.Greetings.Infrastructure",
            "Acme.Modules.Greetings.Endpoints",
        };

        ReferencedAssemblies(GreetingsContracts)
            .Intersect(forbidden)
            .Should()
            .BeEmpty("Application.Contracts may use *.Domain.Contracts, not the module internals");
    }

    [Fact]
    public void Contracts_do_not_depend_on_any_Application_layer()
    {
        // The contract layer may use the shared DTOs (Acme.Kernel.Contracts) and the domain spec
        // interfaces (*.Domain.Contracts), but never an application (handler) layer.
        var forbidden = new[]
        {
            "Acme.Kernel.Application",
            "Acme.Modules.Greetings.Application",
            "Acme.Modules.Widgets.Application",
        };

        foreach (var contracts in new[] { GreetingsContracts, WidgetsContracts })
        {
            ReferencedAssemblies(contracts)
                .Intersect(forbidden)
                .Should()
                .BeEmpty(
                    $"{contracts.GetName().Name} must not reference an application/handler layer"
                );
        }
    }

    // ---- Cross-module isolation: a module may use another module's .Contracts, never its internals ----

    private static readonly string[] ModuleNames = ["Greetings", "Widgets"];

    private static readonly Assembly[] AllModuleAssemblies =
    [
        typeof(Modules.Greetings.Domain.Contracts.AssemblyMarker).Assembly,
        typeof(Modules.Greetings.Application.Contracts.AssemblyMarker).Assembly,
        typeof(Modules.Greetings.Application.AssemblyMarker).Assembly,
        typeof(Modules.Greetings.Infrastructure.AssemblyMarker).Assembly,
        typeof(Modules.Greetings.Endpoints.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Domain.Contracts.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Application.Contracts.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Application.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Infrastructure.AssemblyMarker).Assembly,
        typeof(Modules.Widgets.Endpoints.AssemblyMarker).Assembly,
    ];

    [Fact]
    public void Modules_do_not_reference_other_modules_internals()
    {
        foreach (var assembly in AllModuleAssemblies)
        {
            var name = assembly.GetName().Name!;
            var owner = ModuleNames.Single(m => name.Contains($".Modules.{m}."));
            var forbidden = ModuleNames
                .Where(m => m != owner)
                .SelectMany(m =>
                    new[] { "Domain", "Application", "Infrastructure", "Endpoints" }.Select(layer =>
                        $"Acme.Modules.{m}.{layer}"
                    )
                )
                .ToArray();

            var result = Types
                .InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{name}: {Failures(result)}");
        }
    }

    // ---- Application.Contracts implements domain spec interfaces (Domain.Contracts) only ----

    [Fact]
    public void Application_Contracts_do_not_reference_the_module_Domain_assembly()
    {
        // Command models implement the input spec interfaces in *.Domain.Contracts, but must never
        // reference the module's Domain implementation assembly. Checked at the assembly-reference level
        // because "Acme.Modules.X.Domain" is a namespace prefix of ".Domain.Contracts".
        var cases = new[]
        {
            (
                typeof(Modules.Greetings.Application.Contracts.AssemblyMarker).Assembly,
                "Acme.Modules.Greetings.Domain"
            ),
            (
                typeof(Modules.Widgets.Application.Contracts.AssemblyMarker).Assembly,
                "Acme.Modules.Widgets.Domain"
            ),
        };

        foreach (var (contracts, domainAssemblyName) in cases)
        {
            contracts
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Should()
                .NotContain(
                    domainAssemblyName,
                    $"{contracts.GetName().Name} may implement specs from {domainAssemblyName}.Contracts "
                        + "but must not reference the Domain implementation"
                );
        }
    }

    [Fact]
    public void Architecture_rules_have_teeth()
    {
        // The Api composition root genuinely references module endpoints, so a rule forbidding it
        // MUST report a failure — proving the dependency scan actually detects references.
        var apiDependsOnModules = Types
            .InAssembly(typeof(Api.DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOn("Acme.Modules")
            .GetResult();

        apiDependsOnModules
            .IsSuccessful.Should()
            .BeFalse("the Api references module endpoints; the scan must catch it");
    }

    private static string Failures(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
