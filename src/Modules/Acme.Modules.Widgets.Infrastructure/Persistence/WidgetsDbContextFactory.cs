using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>Used by the EF Core CLI (dotnet ef) at design time only.</summary>
public sealed class WidgetsDbContextFactory : IDesignTimeDbContextFactory<WidgetsDbContext>
{
    public WidgetsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WidgetsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=acme");
        return new WidgetsDbContext(optionsBuilder.Options);
    }
}
