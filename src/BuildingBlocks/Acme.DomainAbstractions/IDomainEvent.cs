namespace Acme.DomainAbstractions;

/// <summary>
/// Marker for a domain event — something that happened inside an aggregate that other parts of the
/// same transaction may react to. Raised by an <see cref="AggregateRoot"/> and dispatched in-process
/// inside the unit of work (same DB transaction). See docs/adr (event-driven decomposition).
/// </summary>
public interface IDomainEvent;
