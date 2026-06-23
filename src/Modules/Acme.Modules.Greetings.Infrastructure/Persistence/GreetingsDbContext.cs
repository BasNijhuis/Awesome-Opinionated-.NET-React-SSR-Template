using Acme.Modules.Greetings.Application.Entities;
using Acme.Modules.Greetings.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acme.Modules.Greetings.Infrastructure.Persistence;

/// <summary>
/// Mapping for the Greetings module's own <c>greetings</c> PostgreSQL schema. Shared by the write
/// context (<see cref="GreetingsDbContext"/>) and the no-tracking read context
/// (<see cref="GreetingsReadDbContext"/>). The strongly-typed id is stored as a uuid column via a
/// value converter.
/// </summary>
public abstract class GreetingsDbContextBase(DbContextOptions options) : DbContext(options)
{
    public const string Schema = "greetings";

    private static readonly ValueConverter<GreetingId, Guid> GreetingIdConverter = new(
        id => id.Value,
        value => new GreetingId(value)
    );

    public DbSet<GreetingEntity> Greetings => Set<GreetingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<GreetingEntity>(entity =>
        {
            entity.ToTable("greetings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasConversion(GreetingIdConverter);
        });
    }
}

/// <summary>Write context for the <c>greetings</c> schema (tracked; owns the migration history).</summary>
public sealed class GreetingsDbContext(DbContextOptions<GreetingsDbContext> options)
    : GreetingsDbContextBase(options);

/// <summary>No-tracking read context for the <c>greetings</c> schema (queries, never the write transaction).</summary>
public sealed class GreetingsReadDbContext(DbContextOptions<GreetingsReadDbContext> options)
    : GreetingsDbContextBase(options);
