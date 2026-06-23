using System.Reflection;
using Acme.CQRS.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.CQRS;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the <see cref="IRequestDispatcher"/> and scans the given assemblies for
    /// command/query handlers and request validators.
    /// </summary>
    public static IServiceCollection AddCqrs(
        this IServiceCollection services,
        params Assembly[] assemblies
    )
    {
        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        foreach (var assembly in assemblies)
        {
            RegisterHandlersAndValidators(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Scans one assembly (e.g. a feature module) for command/query handlers and validators,
    /// without re-registering the dispatcher. Call once per module from its DI extension.
    /// </summary>
    public static IServiceCollection AddCqrsHandlersFrom(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        RegisterHandlersAndValidators(services, assembly);
        return services;
    }

    private static void RegisterHandlersAndValidators(
        IServiceCollection services,
        Assembly assembly
    )
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var serviceType in type.GetInterfaces())
            {
                if (!serviceType.IsGenericType)
                {
                    continue;
                }

                var genericDefinition = serviceType.GetGenericTypeDefinition();
                if (
                    genericDefinition == typeof(ICommandHandler<,>)
                    || genericDefinition == typeof(IQueryHandler<,>)
                    || genericDefinition == typeof(IRequestValidator<>)
                    || genericDefinition == typeof(IDomainEventHandler<>)
                )
                {
                    services.AddScoped(serviceType, type);
                }
            }
        }
    }
}
