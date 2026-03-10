using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class UserPublicProfileConfiguration : IEntityTypeConfiguration<UserPublicProfile>
{
    public void Configure(EntityTypeBuilder<UserPublicProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithOne(x => x.PublicProfile)
            .HasForeignKey<UserPublicProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
