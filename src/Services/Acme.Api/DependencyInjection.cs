using Acme.Api.Realtime;
using Acme.Kernel.Application.Common.Interfaces;
using Acme.Modules.Greetings.Domain.Contracts;
using Acme.Modules.Widgets.Domain.Contracts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Acme.Api;

public static class DependencyInjection
{
    // Strongly-typed IDs serialize as bare GUIDs; present them in the schema as uuid strings so the
    // OpenAPI contract and the generated client treat them as plain uuids. Each module owns its id
    // type — register new ones here so codegen stays clean.
    private static readonly HashSet<Type> StronglyTypedIds = [typeof(GreetingId), typeof(WidgetId)];

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOpenApi(
            "v1",
            options =>
            {
                // Inline the strongly-typed IDs instead of emitting them as named component schemas, so
                // they appear exactly as a bare uuid would (no $ref, no component) — wire/spec unchanged.
                options.CreateSchemaReferenceId = jsonTypeInfo =>
                    StronglyTypedIds.Contains(jsonTypeInfo.Type)
                        ? null
                        : OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);

                options.AddSchemaTransformer(
                    (schema, context, cancellationToken) =>
                    {
                        // Present a typed ID as a uuid string (it serializes as a bare GUID).
                        if (StronglyTypedIds.Contains(context.JsonTypeInfo.Type))
                        {
                            schema.Type = JsonSchemaType.String;
                            schema.Format = "uuid";
                            schema.Pattern = null;
                            schema.Properties?.Clear();
                            return Task.CompletedTask;
                        }

                        // DateTimeOffset/DateTime use a custom JSON converter (canonical-UTC), which
                        // hides their schema from OpenAPI (emits an untyped schema → `unknown` in
                        // generated clients). Present them as ISO date-time strings.
                        if (
                            context.JsonTypeInfo.Type == typeof(DateTimeOffset)
                            || context.JsonTypeInfo.Type == typeof(DateTimeOffset?)
                            || context.JsonTypeInfo.Type == typeof(DateTime)
                            || context.JsonTypeInfo.Type == typeof(DateTime?)
                        )
                        {
                            schema.Type = JsonSchemaType.String;
                            schema.Format = "date-time";
                            schema.Pattern = null;
                            schema.Properties?.Clear();
                            return Task.CompletedTask;
                        }

                        // A collection of typed IDs: present its items as uuid strings.
                        if (
                            ElementType(context.JsonTypeInfo.Type) is { } element
                            && StronglyTypedIds.Contains(element)
                        )
                        {
                            schema.Type = JsonSchemaType.Array;
                            schema.Items = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Format = "uuid",
                            };
                            return Task.CompletedTask;
                        }

                        // .NET emits integer/number properties as ["integer","string"] (with a digit
                        // pattern) under OpenAPI 3.1. Collapse them to the plain numeric type so
                        // generated clients get `number`, not `number | string`.
                        if (
                            schema.Type is { } type
                            && type.HasFlag(JsonSchemaType.String)
                            && (
                                type.HasFlag(JsonSchemaType.Integer)
                                || type.HasFlag(JsonSchemaType.Number)
                            )
                        )
                        {
                            schema.Type = type & ~JsonSchemaType.String;
                            schema.Pattern = null;
                        }

                        return Task.CompletedTask;
                    }
                );
            }
        );
        services.AddSignalR();
        services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();
        return services;
    }

    private static Type? ElementType(Type type) =>
        type == typeof(string)
            ? null
            : type.GetInterfaces()
                .Prepend(type)
                .FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                )
                ?.GetGenericArguments()[0];
}
