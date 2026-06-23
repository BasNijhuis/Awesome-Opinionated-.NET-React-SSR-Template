using Acme.Modules.Widgets.Application;
using Acme.Modules.Widgets.Application.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>
/// EF implementation of the Widgets read context (ADR-0015 + #9/ADR-0016): hands the Application a
/// no-tracking <see cref="IQueryable{T}"/> over the <see cref="WidgetsReadDbContext"/> and runs the
/// EF Core async terminals here, keeping EF out of the Application layer.
/// </summary>
public sealed class EfWidgetsReadContext(WidgetsReadDbContext db) : IWidgetsReadContext
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
/// In-memory implementation of the Widgets read context for the no-database path (ADR-0015): the
/// store holds the persistence entities, so <see cref="Query{T}"/> returns them as a queryable and the
/// terminals run synchronously over LINQ-to-objects, producing views identical to the EF path.
/// </summary>
internal sealed class InMemoryWidgetsReadContext(InMemoryWidgetStore store) : IWidgetsReadContext
{
    public IQueryable<T> Query<T>()
        where T : class
    {
        if (typeof(T) == typeof(WidgetEntity))
        {
            return (IQueryable<T>)store.Widgets.Values.AsQueryable();
        }

        throw new NotSupportedException(
            $"The in-memory Widgets read context has no source for {typeof(T).Name}."
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
