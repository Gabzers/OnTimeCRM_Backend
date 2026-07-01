using System.ComponentModel.DataAnnotations;

namespace OnTime.Application.DTOs.Goals;

public record UserGoalDto(
    Guid Id,
    int MetricType,
    int Period,
    decimal TargetValue,
    bool ShowOnDashboard,
    DateTimeOffset CreatedAt,
    int SortOrder
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
    bool ShowOnDashboard = false
);

public record UpdateUserGoalRequest(
    [Required] decimal TargetValue,
    bool ShowOnDashboard = false
);

public record ReorderGoalsRequest(
    [Required] IReadOnlyList<Guid> OrderedIds
);
