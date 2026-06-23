namespace Acme.Kernel.Application.Common.Interfaces;

/// <summary>
/// Hands the Application layer an <see cref="IQueryable{T}"/> over a module's no-tracking read context
/// so a slice composes its own read projection with <c>System.Linq</c> only (ADR-0015). The EF Core
/// async terminals (<c>ToListAsync</c>/<c>FirstOrDefaultAsync</c>/<c>AnyAsync</c>) stay behind this
/// interface in Infrastructure, so no Application project references <c>Microsoft.EntityFrameworkCore</c>
/// (enforced by an architecture test). Per #9/ADR-0016 each module owns its own read context — see the
/// per-module marker interfaces (e.g. <c>ISessionsReadContext</c>) that DI resolves to the right context.
/// </summary>
public interface IReadDataContext
{
    /// <summary>A no-tracking <see cref="IQueryable{T}"/> source the handler composes its projection on.</summary>
    IQueryable<T> Query<T>()
        where T : class;

    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);
}
