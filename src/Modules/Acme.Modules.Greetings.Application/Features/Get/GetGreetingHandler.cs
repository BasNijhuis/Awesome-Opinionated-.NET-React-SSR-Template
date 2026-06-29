using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Application.Contracts;
using Acme.Modules.Greetings.Application.Entities;

namespace Acme.Modules.Greetings.Application.Features.Get;

/// <summary>
/// Reads a single greeting by composing a flat projection over the module's no-tracking read context
/// (ADR-0015 + #9/ADR-0016) — the query side never loads a tracked write aggregate.
/// </summary>
internal sealed class GetGreetingHandler(IGreetingsReadContext read)
    : IQueryHandler<GetGreetingQuery, GreetingDto>
{
    public async Task<Result<GreetingDto>> HandleAsync(
        GetGreetingQuery query,
        CancellationToken cancellationToken
    )
    {
        var dto = await read.FirstOrDefaultAsync(
            read.Query<GreetingEntity>()
                .Where(g => g.Id == query.Id)
                .Select(g => new GreetingDto
                {
                    Id = g.Id,
                    Message = g.Message,
                    CreatedAt = g.CreatedAt,
                }),
            cancellationToken
        );

        return dto is null ? GreetingErrors.NotFound(query.Id) : dto;
    }
}
