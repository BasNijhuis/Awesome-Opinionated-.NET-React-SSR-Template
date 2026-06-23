using Acme.Modules.Widgets.Application.Entities;
using Acme.Modules.Widgets.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>
/// Mapping for the Widgets module's own <c>widgets</c> PostgreSQL schema. Shared by the write context
/// (<see cref="WidgetsDbContext"/>) and the no-tracking read context
/// (<see cref="WidgetsReadDbContext"/>). The strongly-typed id is stored as a uuid column via a value
/// converter.
/// </summary>
public abstract class WidgetsDbContextBase(DbContextOptions options) : DbContext(options)
{
    public const string Schema = "widgets";

    private static readonly ValueConverter<WidgetId, Guid> WidgetIdConverter = new(
        id => id.Value,
        value => new WidgetId(value)
    );

    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<WidgetEntity>(entity =>
        {
            entity.ToTable("widgets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasConversion(WidgetIdConverter);
        });
    }
}

/// <summary>Write context for the <c>widgets</c> schema (tracked; owns the migration history).</summary>
public sealed class WidgetsDbContext(DbContextOptions<WidgetsDbContext> options)
    : WidgetsDbContextBase(options);

/// <summary>No-tracking read context for the <c>widgets</c> schema (queries, never the write transaction).</summary>
public sealed class WidgetsReadDbContext(DbContextOptions<WidgetsReadDbContext> options)
    : WidgetsDbContextBase(options);
