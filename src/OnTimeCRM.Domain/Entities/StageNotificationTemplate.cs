using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class StageNotificationTemplate : BaseEntity
{
    public Guid StageId { get; set; }
    public Guid UserId { get; set; }  // denormalized for faster queries
    public string Title { get; set; } = string.Empty;
    public int DaysAfter { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? TimeOfDay { get; set; }
    public bool OverridesNewClientNotification { get; set; } = false;

    // Navigation
    public ClientStage Stage { get; set; } = null!;
}
