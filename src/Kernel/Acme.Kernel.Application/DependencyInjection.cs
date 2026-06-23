using System.Reflection;
using Acme.CQRS;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Kernel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registers the dispatcher and scans this assembly for any shared (kernel-level) handlers.
        // Each capability module registers its own handlers from its Add<Module>Module().
        services.AddCqrs(Assembly.GetExecutingAssembly());
        return services;
    }
}
