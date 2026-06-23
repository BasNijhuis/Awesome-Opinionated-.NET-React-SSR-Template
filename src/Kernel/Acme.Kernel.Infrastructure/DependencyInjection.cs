using Acme.CQRS.Abstractions;
using Acme.Kernel.Application.Common.Interfaces;
using Acme.Kernel.Domain.Services;
using Acme.Kernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Acme.Kernel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<TrackedAggregates>();

        if (configuration.GetValue("Persistence:UseInMemory", false))
        {
            services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("acme");
            // The shared scoped connection every module's write context binds to, so the coordinating
            // unit of work commits them in one transaction (#9). A user-initiated transaction rules out
            // a retrying execution strategy, so each module registers its write context as plain
            // AddDbContext bound to this connection; the module read contexts are no-tracking on their
            // own connection (ADR-0015).
            services.AddScoped(_ => new NpgsqlConnection(connectionString));
            services.AddScoped<IUnitOfWork, AcmeUnitOfWork>();
        }

        services.AddSingleton<IRandomProvider, RandomProvider>();
        return services;
    }
}
