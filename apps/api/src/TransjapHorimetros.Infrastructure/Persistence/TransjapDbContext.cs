using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Infrastructure.Identity;
using TransjapHorimetros.Infrastructure.Persistence.Converters;

namespace TransjapHorimetros.Infrastructure.Persistence;

public sealed class TransjapDbContext(DbContextOptions<TransjapDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<MachineQrCode> MachineQrCodes => Set<MachineQrCode>();

    public DbSet<WorkSite> WorkSites => Set<WorkSite>();

    public DbSet<HourMeterReading> HourMeterReadings => Set<HourMeterReading>();

    public DbSet<Anomaly> Anomalies => Set<Anomaly>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PhotoEvidence> PhotoEvidences => Set<PhotoEvidence>();

    public DbSet<PhotoOcrResult> PhotoOcrResults => Set<PhotoOcrResult>();

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
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            b.Property(u => u.IsActive).IsRequired();
            b.Property(u => u.CreatedAt).IsRequired();
            b.Property(u => u.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("roles");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("user_roles"));
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("user_claims"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("user_logins"));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("role_claims"));
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("user_tokens"));

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransjapDbContext).Assembly);
    }

    private void EnsureAppendOnlyEntitiesAreNotModifiedOrDeleted()
    {
        foreach (var entry in ChangeTracker.Entries<PhotoOcrResult>())
        {
            var immutableNames = new[] { nameof(PhotoOcrResult.Id), nameof(PhotoOcrResult.PhotoEvidenceId),
                nameof(PhotoOcrResult.Provider), nameof(PhotoOcrResult.CreatedAt), nameof(PhotoOcrResult.IsInitial),
                nameof(PhotoOcrResult.ActorUserId), nameof(PhotoOcrResult.CorrelationId) };
            if (entry.State == EntityState.Deleted || (entry.State == EntityState.Modified
                && (entry.Property(r => r.Status).OriginalValue != Domain.Enums.PhotoOcrStatus.Processing
                    || immutableNames.Any(name => entry.Property(name).IsModified))))
                throw new InvalidOperationException("Resultados OCR finalizados são imutáveis.");
        }
        if (ChangeTracker.Entries<MachineQrCode>().Any(entry => entry.State == EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "QR codes de máquinas não podem ser excluídos; desative o QR para preservar o histórico.");
        }

        var invalidEntries = ChangeTracker.Entries()
            .Where(entry =>
                (entry.Entity is HourMeterReading or AuditLog or PhotoEvidence)
                && entry.State is EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (invalidEntries.Length > 0)
        {
            throw new InvalidOperationException(
                "Leituras, evidências fotográficas e registros de auditoria são imutáveis; correções devem preservar o histórico.");
        }
    }
}
