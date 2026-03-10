using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Infrastructure.Persistence.Configurations;

public class UserFriendshipConfiguration : IEntityTypeConfiguration<UserFriendship>
{
    public void Configure(EntityTypeBuilder<UserFriendship> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Sender)
            .WithMany(x => x.SentFriendRequests)
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Receiver)
            .WithMany(x => x.ReceivedFriendRequests)
            .HasForeignKey(x => x.ReceiverId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user pair can only have one friendship record
        builder.HasIndex(x => new { x.SenderId, x.ReceiverId }).IsUnique();
    }
}
