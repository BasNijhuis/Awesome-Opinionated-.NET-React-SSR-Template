using Acme.CQRS;
using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Application;
using Acme.Modules.Widgets.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Acme.Modules.Widgets.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the Widgets module's command/query handlers, validators and persistence.</summary>
    public static IServiceCollection AddWidgetsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddCqrsHandlersFrom(typeof(Application.AssemblyMarker).Assembly);

        if (configuration.GetValue("Persistence:UseInMemory", false))
        {
            services.AddSingleton<InMemoryWidgetStore>();
            services.AddScoped<IWidgetRepository, InMemoryWidgetRepository>();
            services.AddScoped<IWidgetsReadContext, InMemoryWidgetsReadContext>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("acme");
            // Write context on the host's shared coordinating-transaction connection; read context on
            // its own no-tracking connection (ADR-0015).
            services.AddDbContext<WidgetsDbContext>(
                (sp, options) => options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
            );
            // Expose the write context as DbContext so the host migrates this schema generically.
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<WidgetsDbContext>());
            services.AddDbContext<WidgetsReadDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            );
            services.AddScoped<IWidgetRepository, EfWidgetRepository>();
            services.AddScoped<IWidgetsReadContext, EfWidgetsReadContext>();
            services.AddScoped<ITransactionalParticipant, WidgetTransactionalParticipant>();
        }

        return services;
    }
}
