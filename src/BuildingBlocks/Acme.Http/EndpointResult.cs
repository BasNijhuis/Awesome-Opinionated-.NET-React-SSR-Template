using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Http;

/// <summary>
/// Standard endpoint response: the success result <typeparamref name="TSuccess"/> (e.g.
/// <c>Ok&lt;T&gt;</c> or <c>Created&lt;T&gt;</c>) plus the shared problem responses
/// (400 validation, 404/409/422 domain) that every module endpoint can return. Implementing
/// <see cref="IEndpointMetadataProvider"/> means OpenAPI documents all of them with no
/// <c>.Produces</c> calls — endpoints just declare <c>EndpointResult&lt;Ok&lt;Dto&gt;&gt;</c>.
/// </summary>
public sealed class EndpointResult<TSuccess> : IResult, IEndpointMetadataProvider
    where TSuccess : IResult, IEndpointMetadataProvider
{
    private static readonly string[] ProblemJson = ["application/problem+json"];

    private readonly IResult _result;

    public EndpointResult(IResult result) => _result = result;

    /// <summary>Lets an inline validation failure (e.g. a bad request body) flow straight to the union.</summary>
    public static implicit operator EndpointResult<TSuccess>(ValidationProblem problem) =>
        new(problem);

    public Task ExecuteAsync(HttpContext httpContext) => _result.ExecuteAsync(httpContext);

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        TSuccess.PopulateMetadata(method, builder);
        builder.Metadata.Add(
            new ProducesResponseTypeMetadata(
                StatusCodes.Status400BadRequest,
                typeof(HttpValidationProblemDetails),
                ProblemJson
            )
        );
        builder.Metadata.Add(
            new ProducesResponseTypeMetadata(
                StatusCodes.Status404NotFound,
                typeof(ProblemDetails),
                ProblemJson
            )
        );
        builder.Metadata.Add(
            new ProducesResponseTypeMetadata(
                StatusCodes.Status409Conflict,
                typeof(ProblemDetails),
                ProblemJson
            )
        );
        builder.Metadata.Add(
            new ProducesResponseTypeMetadata(
                StatusCodes.Status422UnprocessableEntity,
                typeof(ProblemDetails),
                ProblemJson
            )
        );
    }
}
