using System.Data.Common;

namespace Acme.CQRS.Abstractions;

/// <summary>
/// A persistence context that takes part in the coordinating unit of work's single transaction. Each
/// persistent module registers one; the unit of work enlists them all in one shared transaction and
/// saves them together, so a command and the in-process reactions to its domain events commit
/// atomically across module schemas (#9). Implementations wrap their module's EF Core context.
/// </summary>
public interface ITransactionalParticipant
{
    /// <summary>Enlist this context in the shared transaction (all participants share one connection).</summary>
    Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Flush this context's staged changes within the shared transaction (no commit).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
