using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class AnomalyConfiguration : IEntityTypeConfiguration<Anomaly>
{
    public void Configure(EntityTypeBuilder<Anomaly> builder)
    {
        builder.ToTable("anomalies");
        builder.HasKey(anomaly => anomaly.Id).HasName("pk_anomalies");
        builder.Property(anomaly => anomaly.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(anomaly => anomaly.MachineId).HasColumnName("machine_id").IsRequired();
        builder.Property(anomaly => anomaly.ReadingId).HasColumnName("reading_id");
        builder.Property(anomaly => anomaly.Type)
            .HasColumnName("type")
            .HasMaxLength(64)
            .HasConversion(
                type => ToDatabaseValue(type),
                value => FromDatabaseValue<AnomalyType>(value))
            .IsRequired();
        builder.Property(anomaly => anomaly.Severity)
            .HasColumnName("severity")
            .HasMaxLength(16)
            .HasConversion(
                severity => severity.ToString().ToUpperInvariant(),
                value => Enum.Parse<AnomalySeverity>(value, true))
            .IsRequired();
        builder.Property(anomaly => anomaly.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(anomaly => anomaly.Status)
            .HasColumnName("status")
            .HasMaxLength(24)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<AnomalyStatus>(value, true))
            .IsRequired();
        builder.Property(anomaly => anomaly.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne(anomaly => anomaly.Machine)
            .WithMany()
            .HasForeignKey(anomaly => anomaly.MachineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_anomalies_machines_machine_id");

        builder.HasOne(anomaly => anomaly.Reading)
            .WithMany(reading => reading.Anomalies)
            .HasForeignKey(anomaly => anomaly.ReadingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_anomalies_hour_meter_readings_reading_id");

        builder.HasIndex(anomaly => anomaly.ReadingId)
            .HasDatabaseName("ix_anomalies_reading_id");
        builder.HasIndex(anomaly => anomaly.MachineId)
            .HasDatabaseName("ix_anomalies_machine_id");
    }

    private static string ToDatabaseValue(AnomalyType value)
    {
        var name = value.ToString();
        return string.Concat(name.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $"_{character}" : character.ToString()))
            .ToUpperInvariant();
    }

    private static TEnum FromDatabaseValue<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value.Replace("_", string.Empty), true);
}
