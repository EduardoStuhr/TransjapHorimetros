using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class MachineQrCodeConfiguration : IEntityTypeConfiguration<MachineQrCode>
{
    public void Configure(EntityTypeBuilder<MachineQrCode> builder)
    {
        builder.ToTable("machine_qr_codes");
        builder.HasKey(qrCode => qrCode.Id).HasName("pk_machine_qr_codes");
        builder.Property(qrCode => qrCode.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(qrCode => qrCode.MachineId).HasColumnName("machine_id").IsRequired();
        builder.Property(qrCode => qrCode.Token)
            .HasColumnName("token")
            .HasMaxLength(MachineQrCode.MaxTokenLength)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();
        builder.Property(qrCode => qrCode.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(qrCode => qrCode.DeactivatedAt).HasColumnName("deactivated_at");
        builder.Property(qrCode => qrCode.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(qrCode => qrCode.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(qrCode => qrCode.Machine)
            .WithMany(machine => machine.QrCodes)
            .HasForeignKey(qrCode => qrCode.MachineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_machine_qr_codes_machines_machine_id");

        builder.HasIndex(qrCode => qrCode.Token)
            .IsUnique()
            .HasDatabaseName("ux_machine_qr_codes_token");
        builder.HasIndex(qrCode => qrCode.MachineId)
            .IsUnique()
            .HasFilter("[is_active] = 1")
            .HasDatabaseName("ux_machine_qr_codes_machine_active");
    }
}
