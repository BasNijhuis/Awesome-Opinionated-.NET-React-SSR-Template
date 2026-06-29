using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Application.Contracts;
using Acme.Modules.Greetings.Application.Entities;

namespace Acme.Modules.Greetings.Application.Features.List;

/// <summary>
/// Lists all greetings by composing a flat projection over the module's no-tracking read context
/// (ADR-0015 + #9/ADR-0016), newest first.
/// </summary>
internal sealed class ListGreetingsHandler(IGreetingsReadContext read)
    : IQueryHandler<ListGreetingsQuery, IReadOnlyList<GreetingDto>>
{
    public async Task<Result<IReadOnlyList<GreetingDto>>> HandleAsync(
        ListGreetingsQuery query,
        CancellationToken cancellationToken
    )
    {
        var rows = await read.ToListAsync(
            read.Query<GreetingEntity>()
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new GreetingDto
                {
                    Id = g.Id,
                    Message = g.Message,
                    CreatedAt = g.CreatedAt,
                }),
            cancellationToken
        );

        return rows;
    }
}
