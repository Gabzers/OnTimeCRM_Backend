using OnTime.Domain.Common;
using OnTime.Domain.Enums;

namespace OnTime.Domain.Entities;

public class UserGoal : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public GoalMetricType MetricType { get; set; }
    public GoalPeriod Period { get; set; }
    public decimal TargetValue { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>Pinned to the dashboard home screen. At most a handful per user in practice.</summary>
    public bool ShowOnDashboard { get; set; }

    /// <summary>User-controlled display order (lower first) — see ROADMAP.md.</summary>
    public int SortOrder { get; set; }
}
