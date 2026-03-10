using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? SaleId { get; set; }
    public NotificationTrigger Trigger { get; set; } = NotificationTrigger.Manual;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Client? Client { get; set; }
    public Proposal? Proposal { get; set; }
    public Sale? Sale { get; set; }
}
