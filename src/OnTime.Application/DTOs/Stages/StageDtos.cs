using System.ComponentModel.DataAnnotations;

namespace OnTime.Application.DTOs.Stages;

public record ClientStageDto(
    Guid Id,
    string Name,
    string? Color,
    int Order,
    bool IsFinal,
    bool IsWon,
    bool IsLost,
    bool IsActive,
    bool AffectsTemperature,
    bool NotificationsEnabled,
    IEnumerable<StageTemplateDto> Templates,
    IEnumerable<TemperatureRuleDto> TemperatureRules,
    int ClientCount = 0
);

public record StageTemplateDto(
    Guid Id,
    string Title,
    int DaysAfter,
    bool IsEnabled,
    string? TimeOfDay,
    bool OverridesNewClientNotification,
    bool IsRecurring,
    int? RecurrenceIntervalDays,
    int? FixedDayOfWeek,
    int? FixedDayOfMonth,
    int? MaxOccurrences,
    bool SendEmail = false
);

public record TemperatureRuleDto(
    Guid Id,
    int DaysAfterEntry,
    int Temperature
);

public record CreateStageRequest(
    [Required] string Name,
    string? Color,
    bool IsFinal = false,
    bool IsWon = false,
    bool IsLost = false,
    bool AffectsTemperature = false,
    bool NotificationsEnabled = false
);

public record UpdateStageRequest(
    [Required] string Name,
    string? Color,
    bool IsActive,
    bool IsFinal = false,
    bool IsWon = false,
    bool IsLost = false,
    bool AffectsTemperature = false,
    bool NotificationsEnabled = false
);

public record ReorderStagesRequest(IEnumerable<StageOrderItem> Items);
public record StageOrderItem(Guid StageId, int Order);

public record CreateStageTemplateRequest(
    [Required] string Title,
    [Required] int DaysAfter,
    string? TimeOfDay = null,
    bool OverridesNewClientNotification = false,
    bool IsRecurring = false,
    int? RecurrenceIntervalDays = null,
    int? FixedDayOfWeek = null,
    int? FixedDayOfMonth = null,
    int? MaxOccurrences = null,
    bool SendEmail = false
);

public record UpdateStageTemplateRequest(
    [Required] string Title,
    [Required] int DaysAfter,
    bool IsEnabled,
    string? TimeOfDay = null,
    bool OverridesNewClientNotification = false,
    bool IsRecurring = false,
    int? RecurrenceIntervalDays = null,
    int? FixedDayOfWeek = null,
    int? FixedDayOfMonth = null,
    int? MaxOccurrences = null,
    bool SendEmail = false
);

public record CreateTemperatureRuleRequest(
    [Required] int DaysAfterEntry,
    [Required] int Temperature
);

public record UpdateTemperatureRuleRequest(
    [Required] int DaysAfterEntry,
    [Required] int Temperature
);
