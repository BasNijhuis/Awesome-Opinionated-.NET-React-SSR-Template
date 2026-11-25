# ADR-0020: `Result<T>` as a C# union

- **Status:** Accepted
- **Date:** 2026-11-25
- **Amends:** [ADR-0013](./0013-result-objects-cqrs.md) §1, [ADR-0017](./0017-immutable-domain-enforced-by-analyzer.md) (ACME002)

## Context

[ADR-0013](./0013-result-objects-cqrs.md) §1 made `Result` / `Result<T>` records with `IsSuccess` /
`IsFailure`, `Errors`, an `Error` that threw on a success and a `Value` that threw on a failure. Because
`Value` could throw, two analyzers propped it up: **ACME007** flagged a `Value` read without a dominating
success check, and **ACME008** pushed `return` statements onto the implicit conversions. "Handle the
failure" was a custom flow analysis, and every result still had two members that threw in the wrong
state.

C# 15 (the .NET 11 SDK, default for `net11.0`) adds `union` declarations: a closed set of case types the
compiler checks pattern matches against.

## Decision

### 1. The shape

```csharp
public union Result<T>(Success<T>, Failure)
{
    public static implicit operator Result<T>(T value);                   // return value;
    public static implicit operator Result<T>(Error error);               // return error;
    public static implicit operator Result<T>(ImmutableArray<Error> errors); // return errors;
}

public sealed record Success<T>(T Value);

public sealed record Failure   // internal ctor; throws on zero errors
{
    public ImmutableArray<Error> Errors { get; }
    public Error Error => Errors[0];
}
```

- The cases are their own types rather than `T` itself. `union Result<T>(T, Failure)` would be
  ambiguous when `T` is a `Failure` and would fight the `T` conversion handlers rely on.
- A failure still carries **at least one** error. Creating one with none throws, as before.
- `Error` and `ErrorCategory` are unchanged. The non-generic `Result` is gone (only its tests used it).
- The CQRS markers are unchanged: `ICommand<TResult>` / `IQuery<TResult>` carry the success value, so the
  dispatcher still builds a typed failure from `ImmutableArray<Error>` without reflection ([ADR-0019](./0019-reflection-free-dispatch.md)).

### 2. Consuming a result

- **Match with a `switch` expression over both cases.** It's the compiler's exhaustiveness check, and
  **CS8509** (a missing case) and **CS8846** (a case covered only behind a `when`) are errors in every
  project (`Directory.Build.props`, guarded by `BuildSettingsTests`).
- **Passing a failure on** from a handler:
  ```csharp
  return Greeting.Create(command) switch
  {
      Success<Greeting>(var greeting) => await PersistAsync(greeting, cancellationToken),
      Failure failure => failure,
  };
  ```
- **Continuing only after a success** matches the success case:
  `if (created is Success<Greeting>(var greeting)) …`.
- Tests assert with `ShouldSucceed()` / `ShouldFail()` from `Acme.TestSupport`.

### 3. Traps

- A union always has a compiler-generated `object? Value` that holds the **case**, not the success
  value. Reading it compiles, so don't.
- `default(Result<T>)` holds **neither** case and compiles without a warning. It matches no case pattern,
  and an exhaustive `switch` over it throws `SwitchExpressionException`. That is why code never gates on
  `is not Failure`.
- A `switch` **statement** is not checked for exhaustiveness. Use the expression.

### 4. Analyzers

- **ACME002** (a domain type must be a record, …) accepts a C# `union` (`ITypeSymbol.IsUnion`). A
  hand-written `struct` is still flagged.
- **ACME007** and **ACME008** are removed, with their tests and their opt-ins in
  `Directory.Build.targets`. Their ids are **retired**: `RetiredDiagnosticIdsTests` fails if any analyzer
  reports `{prefix}007` or `{prefix}008` again, deriving the prefix so a `setup.cs` rename keeps it valid.
- The analyzer project references Roslyn 5.12 (the first stable union API), so it needs a 5.12+ compiler
  host: SDK 11.0.100, which `global.json` sets as the floor.

## Consequences

### Positive

- Forgetting the failure case is a compile error, not an analyzer heuristic.
- No member can throw because the result is in the wrong state.
- Two analyzers and their flow analysis are gone.
- The API contract is unchanged: the same status codes and `ProblemDetails` / `ValidationProblemDetails`
  shapes, and the same OpenAPI document and generated clients.

### Negative

- The compiler-generated `Value` and `default(Result<T>)` are traps the type system doesn't close, so
  they rely on these docs and the XML docs on `Result<T>`.
- A handler that passes on a failure moves its success path into a helper (`PersistAsync`), because a
  `switch` arm can't `return` from the enclosing method.
- Older ADRs show the pre-union API (`result.Value` in [ADR-0009](./0009-session-persistence-patterns.md)
  and [ADR-0017](./0017-immutable-domain-enforced-by-analyzer.md), `result.IsFailure` in
  [ADR-0013](./0013-result-objects-cqrs.md)). They are historical; this ADR is current.

## Related

- [ADR-0013: Result objects + CQRS](./0013-result-objects-cqrs.md) (amended)
- [ADR-0017: Immutable Domain enforced by an analyzer](./0017-immutable-domain-enforced-by-analyzer.md) (amended)
- [ADR-0019: Reflection-free dispatch](./0019-reflection-free-dispatch.md)
- [backend-development.md § Matching on a result](../instructions/backend-development.md#matching-on-a-result) · [testing.md § Asserting on a `Result`](../instructions/testing.md#asserting-on-a-result)
