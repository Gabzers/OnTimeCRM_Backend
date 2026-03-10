using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.PrimaryColor).HasMaxLength(7);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Company)
            .WithMany(x => x.Brands)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
