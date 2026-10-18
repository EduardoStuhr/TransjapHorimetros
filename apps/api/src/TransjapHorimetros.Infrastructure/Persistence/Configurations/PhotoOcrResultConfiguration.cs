using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class PhotoOcrResultConfiguration : IEntityTypeConfiguration<PhotoOcrResult>
{
    public void Configure(EntityTypeBuilder<PhotoOcrResult> b)
    {
        b.ToTable("photo_ocr_results", t =>
            t.HasCheckConstraint("ck_photo_ocr_confidence", "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)"));
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Provider).HasMaxLength(32);
        b.Property(r => r.ProviderModel).HasMaxLength(100);
        b.Property(r => r.RawText).HasMaxLength(4000);
        b.Property(r => r.DetectedValue).HasPrecision(12, 2);
        b.Property(r => r.Confidence).HasPrecision(9, 8);
        b.Property(r => r.AbsoluteDifference).HasPrecision(12, 2);
        b.Property(r => r.PercentageDifference).HasPrecision(24, 8);
        b.Property(r => r.ErrorCode).HasMaxLength(64);
        b.Property(r => r.ErrorMessage).HasMaxLength(300);
        b.Property(r => r.CorrelationId).HasMaxLength(100);
        b.Property(r => r.Status).HasMaxLength(32).HasConversion(
            s => s.ToString(), s => Enum.Parse<PhotoOcrStatus>(s));
        b.HasOne<PhotoEvidence>().WithMany().HasForeignKey(r => r.PhotoEvidenceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.PhotoEvidenceId, r.CreatedAt });
        b.HasIndex(r => r.ProcessedAtServer);
        b.HasIndex(r => r.Status);
        b.HasIndex(r => r.PhotoEvidenceId).IsUnique().HasFilter("[IsInitial] = 1")
            .HasDatabaseName("ux_photo_ocr_initial");
    }
}
