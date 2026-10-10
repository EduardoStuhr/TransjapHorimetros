using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Infrastructure.Persistence.Converters;

namespace TransjapHorimetros.Infrastructure.Persistence;

public sealed class TransjapDbContext(DbContextOptions<TransjapDbContext> options) : DbContext(options)
{
    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<WorkSite> WorkSites => Set<WorkSite>();

    public DbSet<HourMeterReading> HourMeterReadings => Set<HourMeterReading>();

    public DbSet<Anomaly> Anomalies => Set<Anomaly>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges()
    {
        EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>()
            .HaveColumnType("datetime2(7)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransjapDbContext).Assembly);
    }

    private void EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted()
    {
        var invalidEntries = ChangeTracker.Entries()
            .Where(entry =>
                (entry.Entity is HourMeterReading or AuditLog)
                && entry.State is EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (invalidEntries.Length > 0)
        {
            throw new InvalidOperationException(
                "Leituras originais e registros de auditoria são imutáveis; correções devem ser registradas sem sobrescrever o histórico.");
        }
    }
}
