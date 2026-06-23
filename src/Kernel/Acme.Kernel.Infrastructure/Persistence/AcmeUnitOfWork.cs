using System.Data;
using Acme.CQRS.Abstractions;
using Acme.Kernel.Application.Common.Interfaces;
using Npgsql;

namespace Acme.Kernel.Infrastructure.Persistence;

/// <summary>
/// Coordinating unit of work for the relational path. Every persistent module's write context shares
/// one scoped connection; this opens a single transaction, enlists every
/// <see cref="ITransactionalParticipant"/>, dispatches domain events so cross-module reactions stage
/// their writes, then saves all participants and commits as one atomic transaction (ADR-0016). Any
/// failure rolls the whole thing back.
/// </summary>
public sealed class AcmeUnitOfWork(
    NpgsqlConnection connection,
    IEnumerable<ITransactionalParticipant> participants,
    TrackedAggregates tracked,
    IDomainEventDispatcher dispatcher
) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var enlisted = participants.ToList();

        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var participant in enlisted)
            {
                await participant.EnlistAsync(transaction, cancellationToken);
            }

            // Dispatch domain events (and any they cascade) so reactions stage their writes onto their
            // own contexts; then every participant saves inside this one transaction (D1).
            await DomainEventDrain.DispatchPendingAsync(tracked, dispatcher, cancellationToken);

            foreach (var participant in enlisted)
            {
                await participant.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
