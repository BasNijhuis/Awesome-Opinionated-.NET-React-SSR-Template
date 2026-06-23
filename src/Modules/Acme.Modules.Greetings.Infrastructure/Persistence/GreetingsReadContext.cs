using Acme.Modules.Greetings.Application;
using Acme.Modules.Greetings.Application.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>
/// EF implementation of the Greetings read context (ADR-0015 + #9/ADR-0016): hands the Application a
/// no-tracking <see cref="IQueryable{T}"/> over the <see cref="GreetingsReadDbContext"/> and runs the
/// EF Core async terminals here, keeping EF out of the Application layer.
/// </summary>
public sealed class EfGreetingsReadContext(GreetingsReadDbContext db) : IGreetingsReadContext
{
    public IQueryable<T> Query<T>()
        where T : class => db.Set<T>().AsNoTracking();

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.ToListAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken
    ) => query.FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.AnyAsync(cancellationToken);
}

/// <summary>
/// In-memory implementation of the Greetings read context for the no-database path (ADR-0015): the
/// store holds the persistence entities, so <see cref="Query{T}"/> returns them as a queryable and the
/// terminals run synchronously over LINQ-to-objects, producing views identical to the EF path.
/// </summary>
internal sealed class InMemoryGreetingsReadContext(InMemoryGreetingStore store)
    : IGreetingsReadContext
{
    public IQueryable<T> Query<T>()
        where T : class
    {
        if (typeof(T) == typeof(GreetingEntity))
        {
            return (IQueryable<T>)store.Greetings.Values.AsQueryable();
        }

        throw new NotSupportedException(
            $"The in-memory Greetings read context has no source for {typeof(T).Name}."
        );
    }

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.ToList());

    public Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken
    ) => Task.FromResult(query.FirstOrDefault());

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        Task.FromResult(query.Any());
}
