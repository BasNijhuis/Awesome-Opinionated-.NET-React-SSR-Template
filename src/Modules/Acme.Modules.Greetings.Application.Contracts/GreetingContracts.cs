using Acme.CQRS.Abstractions;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Application.Contracts;

public sealed record CreateGreetingCommand : ICommand<CreateGreetingResult>, ICreateGreetingSpec
{
    public required string Message { get; init; }
}

public sealed record CreateGreetingResult
{
    public required GreetingId Id { get; init; }
    public required GreetingDto Greeting { get; init; }
}

public sealed record GetGreetingQuery : IQuery<GreetingDto>
{
    public required GreetingId Id { get; init; }
}

public sealed record ListGreetingsQuery : IQuery<IReadOnlyList<GreetingDto>>;

/// <summary>Asks the backend for a greeting phrase in the caller's active locale.</summary>
public sealed record SuggestGreetingQuery : IQuery<GreetingSuggestionDto>;

public sealed record GreetingSuggestionDto
{
    public required string Suggestion { get; init; }
}

public sealed record GreetingDto
{
    public required GreetingId Id { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
