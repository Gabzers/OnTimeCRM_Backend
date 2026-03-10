using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Goals;

public record UserGoalDto(
    Guid Id,
    int MetricType,
    int Period,
    decimal TargetValue,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset CreatedAt
);

public record GoalProgressDto(
    UserGoalDto Goal,
    decimal CurrentValue,
    decimal ProgressPct
);

public record CreateUserGoalRequest(
    [Required] int MetricType,
    [Required] int Period,
    [Required] decimal TargetValue,
    [Required] DateTimeOffset StartDate,
    DateTimeOffset? EndDate
);

public record UpdateUserGoalRequest(
    [Required] decimal TargetValue,
    [Required] DateTimeOffset StartDate,
    DateTimeOffset? EndDate
);
