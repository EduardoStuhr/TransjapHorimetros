using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Identity;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class PhotoEvidenceConfiguration : IEntityTypeConfiguration<PhotoEvidence>
{
    public void Configure(EntityTypeBuilder<PhotoEvidence> builder)
    {
        builder.ToTable("photo_evidence", table =>
            table.HasCheckConstraint(
                "ck_photo_evidence_file_size_positive",
                "file_size_bytes > 0"));
        builder.HasKey(evidence => evidence.Id).HasName("pk_photo_evidence");
        builder.Property(evidence => evidence.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(evidence => evidence.ReadingId).HasColumnName("reading_id").IsRequired();
        builder.Property(evidence => evidence.StorageProvider)
            .HasColumnName("storage_provider").HasMaxLength(32).IsRequired();
        builder.Property(evidence => evidence.StorageContainer)
            .HasColumnName("storage_container").HasMaxLength(63).IsRequired();
        builder.Property(evidence => evidence.StorageObjectKey)
            .HasColumnName("storage_object_key").HasMaxLength(512).IsRequired();
        builder.Property(evidence => evidence.OriginalFileName)
            .HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(evidence => evidence.ContentType)
            .HasColumnName("content_type").HasMaxLength(64).IsRequired();
        builder.Property(evidence => evidence.FileSizeBytes)
            .HasColumnName("file_size_bytes").IsRequired();
        builder.Property(evidence => evidence.Sha256)
            .HasColumnName("sha256").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(evidence => evidence.CapturedAtDevice).HasColumnName("captured_at_device");
        builder.Property(evidence => evidence.UploadedAtServer)
            .HasColumnName("uploaded_at_server").IsRequired();
        builder.Property(evidence => evidence.Width).HasColumnName("width");
        builder.Property(evidence => evidence.Height).HasColumnName("height");
        builder.Property(evidence => evidence.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<PhotoEvidenceStatus>(value, true))
            .IsRequired();
        builder.Property(evidence => evidence.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(evidence => evidence.CreatedByEmail)
            .HasColumnName("created_by_email").HasMaxLength(256);
        builder.Property(evidence => evidence.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(evidence => evidence.DeviceId).HasColumnName("device_id").HasMaxLength(100);
        builder.Property(evidence => evidence.ClientEventId).HasColumnName("client_event_id");

        builder.HasOne(evidence => evidence.Reading)
            .WithOne(reading => reading.PhotoEvidence)
            .HasForeignKey<PhotoEvidence>(evidence => evidence.ReadingId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_photo_evidence_hour_meter_readings_reading_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(evidence => evidence.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_photo_evidence_users_created_by_user_id");

        builder.HasIndex(evidence => evidence.ReadingId)
            .IsUnique()
            .HasDatabaseName("ux_photo_evidence_reading_id");
        builder.HasIndex(evidence => evidence.Sha256)
            .HasDatabaseName("ix_photo_evidence_sha256");
        builder.HasIndex(evidence => evidence.CreatedByUserId)
            .HasDatabaseName("ix_photo_evidence_created_by_user_id");
        builder.HasIndex(evidence => evidence.ClientEventId)
            .IsUnique()
            .HasFilter("[client_event_id] IS NOT NULL")
            .HasDatabaseName("ux_photo_evidence_client_event_id");
        builder.HasIndex(evidence => evidence.StorageObjectKey)
            .IsUnique()
            .HasDatabaseName("ux_photo_evidence_storage_object_key");
    }
}
