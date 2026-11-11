using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class ReadingReviewConfiguration : IEntityTypeConfiguration<ReadingReview>
{
    public void Configure(EntityTypeBuilder<ReadingReview> builder)
    {
        builder.ToTable("reading_reviews");
        builder.HasKey(r => r.ReadingId);
        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ActorEmail).HasMaxLength(320).IsRequired();
        builder.HasOne<HourMeterReading>().WithMany().HasForeignKey(r => r.ReadingId).OnDelete(DeleteBehavior.Restrict);
    }
}
