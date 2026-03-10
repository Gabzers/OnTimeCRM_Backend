using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Stages;

public record ClientStageDto(
    Guid Id,
    string Name,
    string? Color,
    int Order,
    bool IsFinal,
    bool IsWon,
    bool IsLost,
    bool IsActive,
    IEnumerable<StageTemplateDto> Templates
);

public record StageTemplateDto(
    Guid Id,
    string Title,
    int DaysAfter,
    bool IsEnabled,
    string? TimeOfDay,
    bool OverridesNewClientNotification
);

public record CreateStageRequest(
    [Required] string Name,
    string? Color
);

public record UpdateStageRequest(
    [Required] string Name,
    string? Color,
    bool IsActive
);

public record ReorderStagesRequest(IEnumerable<StageOrderItem> Items);
public record StageOrderItem(Guid StageId, int Order);

public record CreateStageTemplateRequest(
    [Required] string Title,
    [Required] int DaysAfter,
    string? TimeOfDay = null,
    bool OverridesNewClientNotification = false
);

public record UpdateStageTemplateRequest(
    [Required] string Title,
    [Required] int DaysAfter,
    bool IsEnabled,
    string? TimeOfDay = null,
    bool OverridesNewClientNotification = false
);
