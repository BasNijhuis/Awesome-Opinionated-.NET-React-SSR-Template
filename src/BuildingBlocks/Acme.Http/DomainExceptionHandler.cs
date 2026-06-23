using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Http;

public static class DomainExceptionHandler
{
    // Expected failures flow as Result objects; only genuinely unexpected exceptions reach here.
    public static IApplicationBuilder UseDomainExceptionHandler(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(
                        new ProblemDetails
                        {
                            Title = "Unexpected error",
                            Detail = ex.Message,
                            Status = StatusCodes.Status500InternalServerError,
                        }
                    );
                }
            }
        );
}
