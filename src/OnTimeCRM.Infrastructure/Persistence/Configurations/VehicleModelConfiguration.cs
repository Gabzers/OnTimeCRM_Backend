using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Version).HasMaxLength(100);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.BasePrice).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Brand)
            .WithMany(x => x.Models)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
