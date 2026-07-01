using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

public class StageNotificationTemplate : BaseEntity
{
    public Guid StageId { get; set; }
    public Guid UserId { get; set; }  // denormalized for faster queries
    public string Title { get; set; } = string.Empty;
    public int DaysAfter { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? TimeOfDay { get; set; }
    public bool OverridesNewClientNotification { get; set; } = false;
    public bool SendEmail { get; set; } = false;

    // Recurrence — opt-in, one-shot templates are unaffected
    public bool IsRecurring { get; set; } = false;
    public int? RecurrenceIntervalDays { get; set; }   // repeat every N days after DaysAfter's first fire
    public int? FixedDayOfWeek { get; set; }            // 0=Sunday..6=Saturday — mutually exclusive with the two above/below
    public int? FixedDayOfMonth { get; set; }            // 1-31 — mutually exclusive with FixedDayOfWeek
    public int? MaxOccurrences { get; set; }              // null = unbounded

    // Navigation
    public ClientStage Stage { get; set; } = null!;
}
