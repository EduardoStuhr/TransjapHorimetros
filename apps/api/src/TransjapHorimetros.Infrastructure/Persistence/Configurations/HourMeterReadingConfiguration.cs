using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class HourMeterReadingConfiguration : IEntityTypeConfiguration<HourMeterReading>
{
    public void Configure(EntityTypeBuilder<HourMeterReading> builder)
    {
        builder.ToTable("hour_meter_readings", table =>
            table.HasCheckConstraint("ck_hour_meter_readings_value_non_negative", "value >= 0"));
        builder.HasKey(reading => reading.Id).HasName("pk_hour_meter_readings");
        builder.Property(reading => reading.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(reading => reading.MachineId).HasColumnName("machine_id").IsRequired();
        builder.Property(reading => reading.WorkSiteId).HasColumnName("work_site_id");
        builder.Property(reading => reading.Value)
            .HasColumnName("value")
            .HasPrecision(12, 2)
            .IsRequired();
        builder.Property(reading => reading.ReadingType)
            .HasColumnName("reading_type")
            .HasMaxLength(32)
            .HasConversion(
                type => type.ToString().ToUpperInvariant(),
                value => Enum.Parse<ReadingType>(value, true))
            .IsRequired();
        builder.Property(reading => reading.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<ReadingStatus>(value, true))
            .IsRequired();
        builder.Property(reading => reading.CapturedAtDevice)
            .HasColumnName("captured_at_device")
            .IsRequired();
        builder.Property(reading => reading.ReceivedAtServer)
            .HasColumnName("received_at_server")
            .IsRequired();
        builder.Property(reading => reading.SyncedAt)
            .HasColumnName("synced_at")
            .IsRequired();
        builder.Property(reading => reading.ClientEventId)
            .HasColumnName("client_event_id")
            .IsRequired();
        builder.Property(reading => reading.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(reading => reading.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(reading => reading.Machine)
            .WithMany(machine => machine.Readings)
            .HasForeignKey(reading => reading.MachineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hour_meter_readings_machines_machine_id");
        builder.HasOne(reading => reading.WorkSite)
            .WithMany(workSite => workSite.Readings)
            .HasForeignKey(reading => reading.WorkSiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hour_meter_readings_work_sites_work_site_id");

        builder.HasIndex(reading => reading.ClientEventId)
            .IsUnique()
            .HasDatabaseName("ux_hour_meter_readings_client_event_id");
        builder.HasIndex(reading => new { reading.MachineId, reading.CapturedAtDevice })
            .IsDescending(false, true)
            .HasDatabaseName("ix_hour_meter_readings_machine_captured_at");
        builder.HasIndex(reading => reading.Status)
            .HasDatabaseName("ix_hour_meter_readings_status");
        builder.HasIndex(reading => reading.WorkSiteId)
            .HasDatabaseName("ix_hour_meter_readings_work_site_id");
    }
}
