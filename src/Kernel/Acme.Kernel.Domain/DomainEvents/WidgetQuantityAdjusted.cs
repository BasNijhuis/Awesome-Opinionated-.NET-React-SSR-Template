using Acme.DomainAbstractions;

namespace Acme.Kernel.Domain.DomainEvents;

/// <summary>
/// Raised by the Widgets module's <c>Widget</c> aggregate when its quantity changes. A
/// <strong>cross-module</strong> domain event, so it lives in the shared kernel (not a module) where
/// any module can handle it — the Greetings module reacts by recording an announcement greeting, in
/// the same transaction (ADR-0016, backend-development.md "Domain events &amp; cross-aggregate reactions").
/// <para>
/// The payload is deliberately primitive (no module types): the kernel depends on no module, and a
/// reaction needs only the data to compose its own change — not the originating aggregate.
/// </para>
/// </summary>
public sealed record WidgetQuantityAdjusted(
    Guid WidgetId,
    string Name,
    int OldQuantity,
    int NewQuantity
) : IDomainEvent;
