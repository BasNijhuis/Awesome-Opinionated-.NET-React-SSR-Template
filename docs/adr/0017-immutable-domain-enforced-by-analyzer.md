# ADR-0017: Immutable Domain layer enforced by a Roslyn analyzer

- **Status:** Accepted
- **Date:** 2026-06-21
- **Builds on:** [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md) (event-driven aggregates), [ADR-0013](./0013-result-objects-cqrs.md) (`Result`/`Error`)

## Context

The Domain layer modelled state with mutable aggregates — `private set` properties, non-`readonly` fields, private mutable collections, and in-place mutation methods — while value objects were already immutable `record`s. Mutable aggregate state makes transitions hard to reason about and lets state change outside aggregate methods. We want the Domain **immutable by construction** and that **enforced automatically** so it can't silently regress.

## Decision

### 1. Full functional immutability (no setters)

Every Domain type is immutable. **No `set` accessor exists anywhere in the Domain** (kernel + all modules + `DomainAbstractions`) — state is `{ get; init; }` / `readonly`, and collections are exposed as `IReadOnly…`. Aggregates/entities are **`sealed record`s**; a transition returns a **new** instance built with `with`, e.g.

```csharp
public Result<Widget> AdjustQuantity(int delta)
{
    // …validation…
    var next = this with { Quantity = Quantity + delta };
    return Result.Success(next.RaiseEvent<Widget>(new WidgetQuantityChanged(Id)));
}
```

Transitions return `Result<TAggregate>` (or a tuple where the caller also needs a derived value). Handlers **reassign the returned instance onto the original variable** (`widget = result.Value; repository.Update(widget);`) so the prior instance is unreferenced (GC).

### 2. Domain types are records (no plain classes/structs)

A Domain type must be a `record`, `record struct`, `interface`, or `enum`. Domain services and DI markers (`AssemblyMarker`) are `record`s; `Result`/`Result<T>` are `record`s. The only `class`es allowed are those that **cannot** be records: `static` utility holders and framework-derived classes (`Exception` subtypes, `JsonConverter<T>` subtypes).

### 3. The domain events outbox is immutable too; raising and dequeuing are functional

`AggregateRoot` is an `abstract record` whose outbox is an `init`-only `IReadOnlyList<IDomainEvent> DomainEvents` (no mutable `List`, no copy-constructor). Raising is functional — `protected TSelf RaiseEvent<TSelf>(IDomainEvent)` returns `(TSelf)(this with { DomainEvents = [.. DomainEvents, evt] })`; because the list is immutable, a plain `with` safely shares it, so a transition's new instance carries the prior's still-pending events forward. The aggregate can't clear itself in place, so `DequeueEvents()` is also functional: it returns `(IReadOnlyList<IDomainEvent> Events, AggregateRoot Cleared)` — the pending events plus a cleared copy.

Because aggregates are records, identity tracking can't rely on reference or value equality (a transition yields a new, value-distinct instance of the same aggregate). `AggregateRoot` exposes an abstract **`HasSameIdentity(AggregateRoot)`** (same type + id); `TrackedAggregates.Track` tracks by it and **replaces** a prior instance of the same aggregate with the latest. `TrackedAggregates.DequeueEvents` calls each tracked aggregate's `DequeueEvents()` and **swaps in the returned cleared copy** — so each event dispatches once and a re-drain after a handler cascade sees only freshly raised events. `DomainEventDrain` is unchanged (drains before commit, loops for cascades).

### 4. Enforcement: a custom Roslyn analyzer, build error

A custom analyzer (`Acme.DomainAnalyzers`, netstandard2.0) is wired into every domain-layer project **automatically** by the root `Directory.Build.targets` — keyed off the naming convention (any project ending in `.Domain`, plus the `DomainAbstractions` building block), it injects the analyzer `ProjectReference` (`OutputItemType="Analyzer"`) and the `EnforceImmutability` opt-in (a `CompilerVisibleProperty`). No individual `.csproj` repeats the wiring; a new domain project is covered the moment it's named `*.Domain`. The Domain-layer diagnostics, all **Error** severity:

- **`ACME001`** — a Domain type exposes mutable state: a property with a `set` accessor (any accessibility, incl. `private set`) or a non-`readonly` field. `init`/`readonly` are allowed.
- **`ACME002`** — a Domain type must be a `record`/`record struct`/`interface`/`enum`; a plain `class`/`struct` is flagged unless it is `static` or derives from a non-record base class (framework types).
- **`ACME003`** — no **mutable enumerable type** (`List<>`/`IList<>`/`ICollection<>`/`HashSet<>`/`ISet<>`/`Dictionary<>`/`IDictionary<>`/`Sorted*`/`Queue<>`/`Stack<>`/`T[]`) may appear as a field, property, parameter, or return type of a Domain type; use `IReadOnly…`/`IEnumerable<>`/`System.Collections.Immutable.*`. (Scoped to declared signature/member types, so transient `.ToList()`/`[.. …]` materialization into a read-only shape inside a method body is allowed.)
- **`ACME004`** — guards the self-type pattern: when invoking a generic method with a `TSelf` type parameter (e.g. `RaiseEvent<TSelf>`), the supplied type argument must be the **calling type**, so `Greeting` must call `RaiseEvent<Greeting>` — `RaiseEvent<Widget>` (an `InvalidCastException` at runtime) is a build error.
- **`ACME005`** — a Domain type must not expose a **public `init`** accessor. Domain state is set only inside the declaring type (a constructor/factory plus internal `with`); a public `init` lets callers outside the type set state through an object initializer or `with`, bypassing invariants. `private`/`protected`/`internal` init are allowed. **Compiler-generated accessors are exempt** — positional-record primary-constructor parameters (their declaring syntax is the `ParameterSyntax`), so value objects and events declared positionally are not flagged; their primary constructor is the sanctioned construction path.

A sixth rule guards the persistence boundary in `*.Infrastructure` projects (opt-in via `EnforceEfEntityBoundary`):

- **`ACME006`** — an `*.Infrastructure` project must not map a **domain type** (anything in a `.Domain`/`.DomainAbstractions` assembly/namespace) as an EF entity — i.e. as the type argument of `DbSet<T>`/`Set<T>`/`Entity`/`HasOne`/`HasMany`/`OwnsOne`/`OwnsMany`/`ComplexProperty`/`Navigation`, or `Entity(typeof(T))`. Map the Application-layer `*Entity` POCO instead (ADR-0016/0018). Value-object `Property(…).HasConversion(…)` mappings are exempt.

The opt-in MSBuild property is **`EnforceImmutability`** (Domain projects) / **`EnforceEfEntityBoundary`** (Infrastructure projects); the shared `DiagnosticIds.Prefix` constant (`"ACME"`) backs every id.

Because the diagnostics are errors, `dotnet build` fails on a violation locally; the CI backend build/test job makes that a required check. The analyzer is covered by xUnit v3 + MTP v2 tests (framework-agnostic `CSharpAnalyzerTest<,>` verifier) proving it fires on violations and passes on compliant code.

### 5. Construction convention: non-public constructors + factory methods

To keep the public-`init` ban from simply shifting mutation to public constructors/object initializers, Domain **class** records construct through a non-public constructor plus a `static` factory (`Create`/`Rehydrate`, or the named `Error.NotFound`/etc.). This covers aggregates/entities (e.g. `Greeting`, `Widget`), the cross-module events, and `Error`. Value objects modelled as `readonly record struct` (e.g. `GreetingId`, `WidgetId`) stay positional — C# mandates a public parameterless constructor on every struct, so a non-public ctor is impossible there, and their compiler-generated `init` is ACME005-exempt anyway. A carrier persisted as `jsonb` keeps a `[JsonConstructor]` on its private constructor so `System.Text.Json` deserializes through it without a public surface.

## Consequences

### Positive
- Aggregate state cannot change outside a transition; transitions are explicit and return new state. Accidental mutation is a build error, not a code-review catch.
- Complements the event-driven decomposition (ADR-0016): immutable aggregates + functional transitions + the unchanged event outbox.
- The "records only" rule keeps the model uniform and value-equatable.

### Negative
- More ceremony: transitions return `Result<TAggregate>`/tuples and handlers reassign-and-save; multi-step handlers thread the new instance.
- The analyzer couples to Roslyn internals (versioned with the SDK's 4.x); analyzer tests pin a reference-assembly set.
- A few necessary exemptions (static utilities, exceptions, JSON converters) remain plain classes — documented in the rule.

## Related

- [ADR-0016](./0016-aggregate-decomposition-events-per-module-persistence.md) · [ADR-0013](./0013-result-objects-cqrs.md) · [ADR-0006](./0006-xunit-v3-mtp-v2.md)
