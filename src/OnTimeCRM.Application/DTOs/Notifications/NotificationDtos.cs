using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Notifications;

public record NotificationDto(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    Guid? ProposalId,
    Guid? SaleId,
    int Trigger,
    int Status,
    string Title,
    string? Body,
    DateTimeOffset ScheduledFor,
    DateTimeOffset? DoneAt,
    DateTimeOffset? SnoozedUntil,
    DateTimeOffset CreatedAt
);

public record CreateNotificationRequest(
    Guid? ClientId,
    Guid? ProposalId,
    Guid? SaleId,
    [Required] string Title,
    string? Body,
    [Required] DateTimeOffset ScheduledFor
);

public record SnoozeNotificationRequest(
    [Required] DateTimeOffset SnoozedUntil
);

public record NotificationFilterParams(
    int? Status = null,
    int Page = 1,
    int PageSize = 20
);

public record NotificationPreferenceDto(
    string DailyDigestTime,   // "HH:mm"
    int DigestFrequencyDays,
    int SaleFollowUpDays,
    bool DigestEnabled,
    bool StageChangeNotificationsEnabled,
    bool SaleNotificationsEnabled,
    int? NewClientNotificationDaysAfter,
    string? NewClientNotificationTime
);

public record UpdateNotificationPreferenceRequest(
    string? DailyDigestTime,
    int? DigestFrequencyDays,
    int? SaleFollowUpDays,
    bool? DigestEnabled,
    bool? StageChangeNotificationsEnabled,
    bool? SaleNotificationsEnabled,
    int? NewClientNotificationDaysAfter = null,
    string? NewClientNotificationTime = null
);
