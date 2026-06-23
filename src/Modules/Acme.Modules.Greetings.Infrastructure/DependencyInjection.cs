using Acme.CQRS;
using Acme.CQRS.Abstractions;
using Acme.Modules.Greetings.Application;
using Acme.Modules.Greetings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Acme.Modules.Greetings.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the Greetings module's command/query handlers, validators and persistence.</summary>
    public static IServiceCollection AddGreetingsModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddCqrsHandlersFrom(typeof(Application.AssemblyMarker).Assembly);

        // Locale-aware, DB-free — registered in both modes (it only needs the kernel locale + random
        // ports, like the host registers ILocaleProvider).
        services.AddScoped<IGreetingSuggestionProvider, LocalGreetingSuggestionProvider>();

        if (configuration.GetValue("Persistence:UseInMemory", false))
        {
            services.AddSingleton<InMemoryGreetingStore>();
            services.AddScoped<IGreetingRepository, InMemoryGreetingRepository>();
            services.AddScoped<IGreetingsReadContext, InMemoryGreetingsReadContext>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("acme");
            // Write context on the host's shared coordinating-transaction connection; read context on
            // its own no-tracking connection (ADR-0015).
            services.AddDbContext<GreetingsDbContext>(
                (sp, options) => options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
            );
            // Expose the write context as DbContext so the host migrates this schema generically.
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<GreetingsDbContext>());
            services.AddDbContext<GreetingsReadDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            );
            services.AddScoped<IGreetingRepository, EfGreetingRepository>();
            services.AddScoped<IGreetingsReadContext, EfGreetingsReadContext>();
            services.AddScoped<ITransactionalParticipant, GreetingTransactionalParticipant>();
        }

        return services;
    }
}
