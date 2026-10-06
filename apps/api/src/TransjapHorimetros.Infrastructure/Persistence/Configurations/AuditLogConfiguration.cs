using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(auditLog => auditLog.Id).HasName("pk_audit_logs");
        builder.Property(auditLog => auditLog.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(auditLog => auditLog.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(auditLog => auditLog.Entity).HasColumnName("entity").HasMaxLength(80).IsRequired();
        builder.Property(auditLog => auditLog.EntityId).HasColumnName("entity_id").HasMaxLength(80).IsRequired();
        builder.Property(auditLog => auditLog.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(auditLog => auditLog.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(auditLog => auditLog.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(auditLog => auditLog.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(auditLog => auditLog.UserId).HasColumnName("user_id");
        builder.HasIndex(auditLog => new { auditLog.Entity, auditLog.EntityId })
            .HasDatabaseName("ix_audit_logs_entity_entity_id");
    }
}
