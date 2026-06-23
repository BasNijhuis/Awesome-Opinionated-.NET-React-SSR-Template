using System.Collections.Concurrent;
using Acme.Modules.Greetings.Application.Entities;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>
/// Backing store for the no-database path. It holds the persistence <see cref="GreetingEntity"/> (the
/// row-equivalent), not live aggregates: the repository rehydrates a fresh aggregate from the entity
/// on every read and applies writes back onto it, so the in-memory path mirrors EF's
/// identity-per-load behavior.
/// </summary>
public sealed class InMemoryGreetingStore
{
    internal ConcurrentDictionary<GreetingId, GreetingEntity> Greetings { get; } = new();
}
