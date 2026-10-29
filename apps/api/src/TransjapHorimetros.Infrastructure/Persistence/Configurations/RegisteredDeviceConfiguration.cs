using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;
using TransjapHorimetros.Infrastructure.Identity;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class RegisteredDeviceConfiguration : IEntityTypeConfiguration<RegisteredDevice>
{
    public void Configure(EntityTypeBuilder<RegisteredDevice> builder)
    {
        builder.ToTable("registered_devices", table => table.HasCheckConstraint(
            "ck_registered_devices_status", "status IN ('PENDING', 'AUTHORIZED', 'REVOKED')"));
        builder.HasKey(device => device.Id).HasName("pk_registered_devices");
        builder.Property(device => device.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(device => device.LocalDeviceId).HasColumnName("local_device_id").HasMaxLength(100).IsRequired();
        builder.Property(device => device.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(device => device.Platform).HasColumnName("platform").HasMaxLength(50).IsRequired();
        builder.Property(device => device.Status).HasColumnName("status").HasMaxLength(16)
            .HasConversion(status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<RegisteredDeviceStatus>(value, true)).IsRequired()
            .IsConcurrencyToken();
        builder.Property(device => device.PublicKey).HasColumnName("public_key").HasMaxLength(1024).IsRequired();
        builder.Property(device => device.PublicKeyFingerprint).HasColumnName("public_key_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(device => device.KeyAlgorithm).HasColumnName("key_algorithm").HasMaxLength(50).IsRequired();
        builder.Property(device => device.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(device => device.RequestedByEmail).HasColumnName("requested_by_email").HasMaxLength(320).IsRequired();
        builder.Property(device => device.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(device => device.AuthorizedByUserId).HasColumnName("authorized_by_user_id");
        builder.Property(device => device.AuthorizedByEmail).HasColumnName("authorized_by_email").HasMaxLength(320);
        builder.Property(device => device.AuthorizedAt).HasColumnName("authorized_at");
        builder.Property(device => device.RevokedByUserId).HasColumnName("revoked_by_user_id");
        builder.Property(device => device.RevokedByEmail).HasColumnName("revoked_by_email").HasMaxLength(320);
        builder.Property(device => device.RevokedAt).HasColumnName("revoked_at");
        builder.Property(device => device.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(device => device.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(device => device.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(device => device.LocalDeviceId).IsUnique().HasDatabaseName("ux_registered_devices_local_device_id");
        builder.HasIndex(device => device.PublicKeyFingerprint).IsUnique().HasDatabaseName("ux_registered_devices_key_fingerprint");
        builder.HasIndex(device => new { device.Status, device.RequestedAt }).HasDatabaseName("ix_registered_devices_status_requested_at");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(device => device.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(device => device.AuthorizedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(device => device.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
