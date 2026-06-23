using Acme.CQRS.Abstractions;
using Acme.Http;
using Acme.Modules.Widgets.Application.Contracts;
using Acme.Modules.Widgets.Domain.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Acme.Modules.Widgets.Endpoints;

public static class WidgetsEndpoints
{
    public static IEndpointRouteBuilder MapWidgetsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/widgets");

        group.MapPost("/", CreateWidget).WithName(nameof(CreateWidget)).ProducesDomainProblems();
        group.MapGet("/{id:guid}", GetWidget).WithName(nameof(GetWidget)).ProducesDomainProblems();
        group.MapGet("/", ListWidgets).WithName(nameof(ListWidgets)).ProducesDomainProblems();

        return app;
    }

    private static async Task<EndpointResult<Created<CreateWidgetResult>>> CreateWidget(
        CreateWidgetRequest body,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.SendAsync(
            new CreateWidgetCommand { Name = body.Name, Quantity = body.Quantity },
            cancellationToken
        );
        return result.ToCreated(r => $"/api/widgets/{r.Id.Value}");
    }

    private static async Task<EndpointResult<Ok<WidgetDto>>> GetWidget(
        Guid id,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.QueryAsync(
            new GetWidgetQuery { Id = new WidgetId(id) },
            cancellationToken
        );
        return result.ToOk();
    }

    private static async Task<EndpointResult<Ok<IReadOnlyList<WidgetDto>>>> ListWidgets(
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var result = await dispatcher.QueryAsync(new ListWidgetsQuery(), cancellationToken);
        return result.ToOk();
    }
}

public sealed record CreateWidgetRequest(string Name, int Quantity);
