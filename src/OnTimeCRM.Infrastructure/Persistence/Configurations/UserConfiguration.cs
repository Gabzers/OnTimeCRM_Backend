using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(254);
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.StripeCustomerId).HasMaxLength(100);
        builder.Property(x => x.StripeSubscriptionId).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.Email).IsUnique();

        builder.HasOne(x => x.Company)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.Brand)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
