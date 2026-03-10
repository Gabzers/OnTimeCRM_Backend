using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.TaxId).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Website).HasMaxLength(200);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
    }
}
