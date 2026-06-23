using Acme.CQRS.Abstractions;
using Acme.Modules.Greetings.Application;
using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

public sealed class EfGreetingRepository(GreetingsDbContext db, TrackedAggregates tracked)
    : IGreetingRepository
{
    public void Add(Greeting greeting)
    {
        db.Greetings.Add(GreetingPersistenceMapper.ToEntity(greeting));
        tracked.Track(greeting);
    }

    public async Task<Greeting?> GetByIdAsync(GreetingId id, CancellationToken cancellationToken)
    {
        var entity =
            db.Greetings.Local.FirstOrDefault(g => g.Id == id)
            ?? await db.Greetings.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        return entity is null ? null : GreetingPersistenceMapper.ToDomain(entity);
    }
}
