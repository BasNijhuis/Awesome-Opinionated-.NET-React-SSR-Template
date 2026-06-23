using Acme.CQRS.Abstractions;
using Acme.Http;
using Acme.Modules.Greetings.Application.Contracts;
using Acme.Modules.Greetings.Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Acme.Modules.Greetings.Endpoints;

public static class GreetingsEndpoints
{
    public static IEndpointRouteBuilder MapGreetingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/greetings");

        group
            .MapPost("/", CreateGreeting)
            .WithName(nameof(CreateGreeting))
            .ProducesDomainProblems();
        group
            .MapGet("/{id:guid}", GetGreeting)
            .WithName(nameof(GetGreeting))
            .ProducesDomainProblems();
        group.MapGet("/", ListGreetings).WithName(nameof(ListGreetings)).ProducesDomainProblems();
        group
            .MapGet("/suggestion", SuggestGreeting)
            .WithName(nameof(SuggestGreeting))
            .ProducesDomainProblems();

        return app;
    }

    private static async Task<EndpointResult<Ok<GreetingSuggestionDto>>> SuggestGreeting(
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        // No locale parameter: the API derives it from Accept-Language (ILocaleProvider), which the SSR
        // forwards from the user's chosen language.
        var result = await dispatcher.QueryAsync(new SuggestGreetingQuery(), cancellationToken);
        return result.ToOk();
    }

    private static async Task<EndpointResult<Created<CreateGreetingResult>>> CreateGreeting(
        CreateGreetingRequest body,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.SendAsync(
            new CreateGreetingCommand { Message = body.Message },
            cancellationToken
        );
        return result.ToCreated(r => $"/api/greetings/{r.Id.Value}");
    }

    private static async Task<EndpointResult<Ok<GreetingDto>>> GetGreeting(
        Guid id,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.QueryAsync(
            new GetGreetingQuery { Id = new GreetingId(id) },
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<EndpointResult<Ok<IReadOnlyList<GreetingDto>>>> ListGreetings(
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.QueryAsync(new ListGreetingsQuery(), cancellationToken);
        return result.ToOk();
    }
}

public sealed record CreateGreetingRequest(string Message);
