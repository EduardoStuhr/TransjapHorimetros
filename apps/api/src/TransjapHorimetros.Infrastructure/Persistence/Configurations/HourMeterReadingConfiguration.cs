using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Identity;

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
        builder.Property(r => r.MeterUnit).HasColumnName("meter_unit").HasMaxLength(16)
            .HasConversion(v => v.HasValue ? v.Value.ToString().ToUpperInvariant() : null,
                v => v == null ? null : Enum.Parse<MeterUnit>(v, true));
        builder.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(r => r.CreatedByEmail).HasColumnName("created_by_email").HasMaxLength(320);
        builder.Property(r => r.DeviceId).HasColumnName("device_id").HasMaxLength(100);
        builder.Property(r => r.PreviousReadingId).HasColumnName("previous_reading_id");
        builder.Property(r => r.DifferenceFromPrevious).HasColumnName("difference_from_previous").HasPrecision(12, 2);
        builder.Property(r => r.CorrectionOfReadingId).HasColumnName("correction_of_reading_id");
        builder.HasOne<HourMeterReading>().WithMany().HasForeignKey(r => r.PreviousReadingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HourMeterReading>().WithMany(r => r.Corrections).HasForeignKey(r => r.CorrectionOfReadingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("hour_meter_readings", table =>
        {
            table.HasCheckConstraint("ck_readings_previous_pair", "(previous_reading_id IS NULL AND difference_from_previous IS NULL) OR (previous_reading_id IS NOT NULL AND difference_from_previous IS NOT NULL AND previous_reading_id <> id)");
            table.HasCheckConstraint("ck_readings_correction_link", "correction_of_reading_id IS NULL OR (reading_type = 'CORRECTION' AND correction_of_reading_id <> id)");
            table.HasCheckConstraint("ck_readings_meter_unit", "meter_unit IS NULL OR meter_unit IN ('HOURS', 'KM')");
        });
        builder.HasIndex(r => new { r.MachineId, r.MeterUnit, r.CapturedAtDevice, r.ReceivedAtServer, r.Id })
            .IsDescending(false, false, true, true, true).HasDatabaseName("ix_readings_operational_order");
        builder.HasIndex(r => new { r.CapturedAtDevice, r.ReceivedAtServer, r.Id })
            .IsDescending(true, true, true).HasDatabaseName("ix_readings_capture_order");
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
