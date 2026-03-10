using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class UserGoal : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public GoalMetricType MetricType { get; set; }
    public GoalPeriod Period { get; set; }
    public decimal TargetValue { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}
