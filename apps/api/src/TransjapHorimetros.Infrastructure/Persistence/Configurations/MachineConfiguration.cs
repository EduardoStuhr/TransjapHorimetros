using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("machines", table =>
            table.HasCheckConstraint("ck_machines_fleet_number_positive", "fleet_number > 0"));
        builder.HasKey(machine => machine.Id).HasName("pk_machines");
        builder.Property(machine => machine.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(machine => machine.FleetNumber).HasColumnName("fleet_number").IsRequired();
        builder.Property(machine => machine.Model).HasColumnName("model").HasMaxLength(160).IsRequired();
        builder.Property(machine => machine.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<MachineStatus>(value, true))
            .IsRequired();
        builder.Property(machine => machine.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(machine => machine.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.HasIndex(machine => machine.FleetNumber)
            .IsUnique()
            .HasDatabaseName("ux_machines_fleet_number");
    }
}
