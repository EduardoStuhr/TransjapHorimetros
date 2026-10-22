using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class DeviceChallengeConfiguration : IEntityTypeConfiguration<DeviceChallenge>
{
    public void Configure(EntityTypeBuilder<DeviceChallenge> builder)
    {
        builder.ToTable("device_challenges");
        builder.HasKey(challenge => challenge.Id).HasName("pk_device_challenges");
        builder.Property(challenge => challenge.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(challenge => challenge.RegisteredDeviceId).HasColumnName("registered_device_id").IsRequired();
        builder.Property(challenge => challenge.Nonce).HasColumnName("nonce").HasMaxLength(100).IsRequired();
        builder.Property(challenge => challenge.CanonicalPayload).HasColumnName("canonical_payload").HasMaxLength(500).IsRequired();
        builder.Property(challenge => challenge.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(challenge => challenge.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(challenge => challenge.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(challenge => challenge.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(challenge => challenge.Nonce).IsUnique().HasDatabaseName("ux_device_challenges_nonce");
        builder.HasIndex(challenge => new { challenge.RegisteredDeviceId, challenge.ExpiresAt })
            .HasDatabaseName("ix_device_challenges_device_expiry");
        builder.HasOne(challenge => challenge.RegisteredDevice).WithMany(device => device.Challenges)
            .HasForeignKey(challenge => challenge.RegisteredDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
