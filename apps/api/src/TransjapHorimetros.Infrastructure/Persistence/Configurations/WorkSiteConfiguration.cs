using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransjapHorimetros.Domain.Entities;

namespace TransjapHorimetros.Infrastructure.Persistence.Configurations;

public sealed class WorkSiteConfiguration : IEntityTypeConfiguration<WorkSite>
{
    public void Configure(EntityTypeBuilder<WorkSite> builder)
    {
        builder.ToTable("work_sites");
        builder.HasKey(workSite => workSite.Id).HasName("pk_work_sites");
        builder.Property(workSite => workSite.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(workSite => workSite.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(workSite => workSite.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        builder.Property(workSite => workSite.Active).HasColumnName("active").IsRequired();
        builder.Property(workSite => workSite.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(workSite => workSite.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.HasIndex(workSite => workSite.Code).HasDatabaseName("ix_work_sites_code");
    }
}
