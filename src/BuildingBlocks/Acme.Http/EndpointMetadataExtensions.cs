using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Acme.Http;

public static class EndpointMetadataExtensions
{
    /// <summary>Declares the shared 404/409/422 problem responses produced by <c>ToHttpResult</c>.</summary>
    public static RouteHandlerBuilder ProducesDomainProblems(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
