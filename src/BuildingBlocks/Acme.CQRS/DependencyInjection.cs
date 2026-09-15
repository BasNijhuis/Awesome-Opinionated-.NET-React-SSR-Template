using System.Reflection;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
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

    /// <summary>
    /// Registers a single command handler and its dispatch adapter. Use this instead of
    /// <c>AddScoped&lt;ICommandHandler&lt;,&gt;, THandler&gt;()</c> — a handler without an adapter is
    /// invisible to the dispatcher.
    /// </summary>
    public static IServiceCollection AddCommandHandler<THandler, TCommand, TResult>(
        this IServiceCollection services
    )
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        services.AddScoped<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddKeyedScoped<
            ICommandHandlerAdapter<TResult>,
            CommandHandlerAdapter<TCommand, TResult>
        >(typeof(TCommand));
        return services;
    }

    /// <inheritdoc cref="AddCommandHandler{THandler,TCommand,TResult}"/>
    public static IServiceCollection AddQueryHandler<THandler, TQuery, TResult>(
        this IServiceCollection services
    )
        where THandler : class, IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        services.AddScoped<IQueryHandler<TQuery, TResult>, THandler>();
        services.AddKeyedScoped<
            IQueryHandlerAdapter<TResult>,
            QueryHandlerAdapter<TQuery, TResult>
        >(typeof(TQuery));
        return services;
    }

    /// <inheritdoc cref="AddCommandHandler{THandler,TCommand,TResult}"/>
    public static IServiceCollection AddDomainEventHandler<THandler, TEvent>(
        this IServiceCollection services
    )
        where THandler : class, IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
        AddDomainEventAdapter(services, typeof(TEvent), typeof(DomainEventHandlerAdapter<TEvent>));
        return services;
    }

    /// <summary>
    /// Fails fast if any registered handler has no dispatch adapter — which happens when a handler is
    /// registered directly (<c>AddScoped&lt;IDomainEventHandler&lt;T&gt;, H&gt;()</c>) instead of through
    /// a scan or the <c>Add*Handler</c> helpers. A command or query would throw on first dispatch, but a
    /// domain-event handler would be <em>silently skipped</em>, dropping an in-transaction reaction
    /// (ADR-0016). Call once at the composition root, after every module has registered.
    /// </summary>
    public static IServiceCollection ValidateCqrsRegistrations(this IServiceCollection services)
    {
        var orphans = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType)
            .Select(descriptor =>
                (
                    descriptor.ServiceType,
                    Definition: descriptor.ServiceType.GetGenericTypeDefinition()
                )
            )
            .Where(pair => AdapterFor(pair.Definition) is not null)
            .Where(pair => !HasAdapter(services, pair.ServiceType, pair.Definition))
            .Select(pair => DescribeOrphan(pair.ServiceType, pair.Definition))
            .Distinct()
            .ToList();

        if (orphans.Count > 0)
        {
            throw new InvalidOperationException(
                "These handlers are registered without a dispatch adapter and would never run: "
                    + string.Join("; ", orphans)
                    + ". Register them with AddCqrsHandlersFrom(assembly) or the Add*Handler helpers."
            );
        }

        return services;
    }

    private static Type? AdapterFor(Type genericDefinition) =>
        genericDefinition == typeof(ICommandHandler<,>) ? typeof(ICommandHandlerAdapter<>)
        : genericDefinition == typeof(IQueryHandler<,>) ? typeof(IQueryHandlerAdapter<>)
        : genericDefinition == typeof(IDomainEventHandler<>) ? typeof(IDomainEventHandlerAdapter)
        : null;

    private static bool HasAdapter(
        IServiceCollection services,
        Type handlerServiceType,
        Type genericDefinition
    )
    {
        var arguments = handlerServiceType.GetGenericArguments();
        var adapterServiceType =
            genericDefinition == typeof(IDomainEventHandler<>)
                ? typeof(IDomainEventHandlerAdapter)
                : AdapterFor(genericDefinition)!.MakeGenericType(arguments[1]);

        return services.Any(descriptor =>
            descriptor.ServiceType == adapterServiceType
            && Equals(descriptor.ServiceKey, arguments[0])
        );
    }

    private static string DescribeOrphan(Type handlerServiceType, Type genericDefinition)
    {
        var arguments = handlerServiceType.GetGenericArguments();
        var kind =
            genericDefinition == typeof(ICommandHandler<,>) ? "command"
            : genericDefinition == typeof(IQueryHandler<,>) ? "query"
            : "domain event";

        return $"{kind} {arguments[0].Name}";
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
                    RegisterDispatchAdapter(services, serviceType, genericDefinition);
                }
            }
        }
    }

    /// <summary>
    /// Registers the closed adapter that lets the dispatcher reach this handler without reflection,
    /// keyed by the request/event type it handles. This is the one place a generic type is closed at
    /// runtime; dispatch itself is a plain virtual call (ADR-0019).
    /// </summary>
    private static void RegisterDispatchAdapter(
        IServiceCollection services,
        Type serviceType,
        Type genericDefinition
    )
    {
        var arguments = serviceType.GetGenericArguments();

        if (genericDefinition == typeof(ICommandHandler<,>))
        {
            services.AddKeyedScoped(
                typeof(ICommandHandlerAdapter<>).MakeGenericType(arguments[1]),
                arguments[0],
                typeof(CommandHandlerAdapter<,>).MakeGenericType(arguments)
            );
        }
        else if (genericDefinition == typeof(IQueryHandler<,>))
        {
            services.AddKeyedScoped(
                typeof(IQueryHandlerAdapter<>).MakeGenericType(arguments[1]),
                arguments[0],
                typeof(QueryHandlerAdapter<,>).MakeGenericType(arguments)
            );
        }
        else if (genericDefinition == typeof(IDomainEventHandler<>))
        {
            AddDomainEventAdapter(
                services,
                arguments[0],
                typeof(DomainEventHandlerAdapter<>).MakeGenericType(arguments)
            );
        }
    }

    /// <summary>
    /// Adds the event adapter unless one is already registered for this event type. The adapter fans
    /// out to every handler for the event, so one per event type is both necessary and sufficient —
    /// two modules handling the same kernel event must not each add their own.
    /// </summary>
    private static void AddDomainEventAdapter(
        IServiceCollection services,
        Type eventType,
        Type adapterType
    )
    {
        var alreadyRegistered = services.Any(descriptor =>
            descriptor.ServiceType == typeof(IDomainEventHandlerAdapter)
            && Equals(descriptor.ServiceKey, eventType)
        );

        if (!alreadyRegistered)
        {
            services.AddKeyedScoped(typeof(IDomainEventHandlerAdapter), eventType, adapterType);
        }
    }
}
