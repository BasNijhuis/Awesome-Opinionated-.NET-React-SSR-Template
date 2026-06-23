using System.Data.Common;
using Acme.CQRS.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>Enlists and saves the Greetings module's context in the coordinating transaction.</summary>
public sealed class GreetingTransactionalParticipant(GreetingsDbContext context)
    : ITransactionalParticipant
{
    public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken) =>
        context.Database.UseTransactionAsync(transaction, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
