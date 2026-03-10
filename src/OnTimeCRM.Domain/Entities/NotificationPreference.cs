using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }   // unique — one per user
    public TimeOnly DailyDigestTime { get; set; } = new TimeOnly(9, 29);
    public int DigestFrequencyDays { get; set; } = 2;
    public int SaleFollowUpDays { get; set; } = 30;
    public bool DigestEnabled { get; set; } = true;
    public bool StageChangeNotificationsEnabled { get; set; } = true;
    public bool SaleNotificationsEnabled { get; set; } = true;
    public int? NewClientNotificationDaysAfter { get; set; } = 2;
    public string? NewClientNotificationTime { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
