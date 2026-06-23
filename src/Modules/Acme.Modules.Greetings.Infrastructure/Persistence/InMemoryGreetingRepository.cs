using Acme.CQRS.Abstractions;
using Acme.Modules.Greetings.Application;
using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>
/// No-database repository that mirrors <see cref="EfGreetingRepository"/>: reads rehydrate a fresh
/// aggregate from the stored entity, and Add maps the aggregate back onto an entity in the store right
/// away (the entity is the working set, like EF's tracked entity).
/// </summary>
public sealed class InMemoryGreetingRepository(
    InMemoryGreetingStore store,
    TrackedAggregates tracked
) : IGreetingRepository
{
    public void Add(Greeting greeting)
    {
        var entity = GreetingPersistenceMapper.ToEntity(greeting);
        if (!store.Greetings.TryAdd(greeting.Id, entity))
        {
            throw new InvalidOperationException("Greeting id collision.");
        }

        tracked.Track(greeting);
    }

    public Task<Greeting?> GetByIdAsync(GreetingId id, CancellationToken cancellationToken)
    {
        var entity = store.Greetings.GetValueOrDefault(id);
        return Task.FromResult(entity is null ? null : GreetingPersistenceMapper.ToDomain(entity));
    }
}
