using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class Client : BaseEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
    public LeadSource LeadSource { get; set; } = LeadSource.WalkIn;
    public Guid CurrentStageId { get; set; }
    public DealTemperature Temperature { get; set; } = DealTemperature.Warm;
    public DateTimeOffset? LastInteractionAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ClientStage CurrentStage { get; set; } = null!;
    public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public ICollection<ClientStageHistory> StageHistory { get; set; } = new List<ClientStageHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
