using Acme.DomainAbstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Http;

/// <summary>
/// Maps a <see cref="Result{T}"/> to an <see cref="EndpointResult{TSuccess}"/>, translating
/// <see cref="ErrorCategory"/> to the 404/409/422 problem-detail shapes. Validation failures carry
/// a <see cref="ValidationProblemDetails"/> body so the field map is preserved at runtime.
/// </summary>
public static class ResultHttpExtensions
{
    public static EndpointResult<Ok<T>> ToOk<T>(this Result<T> result) =>
        new(result.IsSuccess ? TypedResults.Ok(result.Value) : ToProblem(result));

    public static EndpointResult<Created<T>> ToCreated<T>(
        this Result<T> result,
        Func<T, string> location
    ) =>
        new(
            result.IsSuccess
                ? TypedResults.Created(location(result.Value), result.Value)
                : ToProblem(result)
        );

    private static IResult ToProblem(Result result)
    {
        if (result.Error.Category == ErrorCategory.Validation)
        {
            var fields = result
                .Errors.GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return TypedResults.UnprocessableEntity<ProblemDetails>(
                new ValidationProblemDetails(fields)
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    // Stable code for the SSR layer to translate, alongside the per-field map above.
                    Extensions = { ["errorCode"] = result.Error.Code },
                }
            );
        }

        var (status, title) = result.Error.Category switch
        {
            ErrorCategory.NotFound => (StatusCodes.Status404NotFound, "Session not found"),
            ErrorCategory.Conflict => (StatusCodes.Status409Conflict, "Invalid session state"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Domain rule violation"),
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Error.Message,
            // Stable, locale-agnostic identifier the web maps to a localized message; `Detail` stays
            // English for logs and as the SSR fallback for any code without a translation.
            Extensions = { ["errorCode"] = result.Error.Code },
        };

        return result.Error.Category switch
        {
            ErrorCategory.NotFound => TypedResults.NotFound(problem),
            ErrorCategory.Conflict => TypedResults.Conflict(problem),
            _ => TypedResults.UnprocessableEntity(problem),
        };
    }
}
