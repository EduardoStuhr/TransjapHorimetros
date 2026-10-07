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
}
