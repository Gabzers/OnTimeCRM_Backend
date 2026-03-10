using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class UserFriendship : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    // Navigation
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
