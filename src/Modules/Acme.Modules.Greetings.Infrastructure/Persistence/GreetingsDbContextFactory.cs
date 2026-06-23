using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>Used by the EF Core CLI (dotnet ef) at design time only.</summary>
public sealed class GreetingsDbContextFactory : IDesignTimeDbContextFactory<GreetingsDbContext>
{
    public GreetingsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GreetingsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=acme");
        return new GreetingsDbContext(optionsBuilder.Options);
    }
}
