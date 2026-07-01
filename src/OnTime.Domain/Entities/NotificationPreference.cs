using OnTime.Domain.Common;
using OnTime.Domain.Enums;

namespace OnTime.Domain.Entities;

public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }   // unique — one per user
    public TimeOnly DailyDigestTime { get; set; } = new TimeOnly(9, 29);
    public int DigestFrequencyDays { get; set; } = 2;
    public int DigestDaysOfWeek { get; set; } = 0;  // bitmask: bit0=Sun,1=Mon,...,6=Sat; 0=use frequency
    public int SaleFollowUpDays { get; set; } = 30;
    public bool DigestEnabled { get; set; } = true;
    public bool StageChangeNotificationsEnabled { get; set; } = true;
    public bool SaleNotificationsEnabled { get; set; } = true;
    public int? NewClientNotificationDaysAfter { get; set; } = 2;
    public string? NewClientNotificationTime { get; set; }
    public bool EmailOnFriendRequests { get; set; } = true;
    public bool EmailOnGeneralNotifications { get; set; } = true;
    public DateTimeOffset? LastDigestSentAt { get; set; }

    // Business summary email — a 3rd, separate email from the pending-notifications digest
    // above: aggregate counts/stage snapshot/goals progress, sent weekly or monthly.
    public bool BusinessSummaryEnabled { get; set; } = true;
    public SummaryFrequency BusinessSummaryFrequency { get; set; } = SummaryFrequency.Weekly;
    /// <summary>0=Sunday..6=Saturday (System.DayOfWeek numbering). Weekly: delivery day every
    /// week. Monthly: delivered on the first occurrence of this weekday each month.</summary>
    public int BusinessSummaryDayOfWeek { get; set; } = 1; // Monday
    public bool BusinessSummaryIncludeCounts { get; set; } = true;
    public bool BusinessSummaryIncludeStageSummary { get; set; } = true;
    public bool BusinessSummaryIncludeGoals { get; set; } = true;
    public DateTimeOffset? LastBusinessSummarySentAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
