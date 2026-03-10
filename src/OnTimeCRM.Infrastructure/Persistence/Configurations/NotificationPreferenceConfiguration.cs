using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.NotificationPreference)
            .HasForeignKey<NotificationPreference>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
