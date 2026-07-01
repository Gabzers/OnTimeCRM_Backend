using System.ComponentModel.DataAnnotations;

namespace OnTime.Application.DTOs.Notifications;

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
    int DigestDaysOfWeek,
    int SaleFollowUpDays,
    bool DigestEnabled,
    bool StageChangeNotificationsEnabled,
    bool SaleNotificationsEnabled,
    int? NewClientNotificationDaysAfter,
    string? NewClientNotificationTime,
    bool EmailOnFriendRequests,
    bool EmailOnGeneralNotifications,
    bool BusinessSummaryEnabled,
    int BusinessSummaryFrequency,
    int BusinessSummaryDayOfWeek,
    bool BusinessSummaryIncludeCounts,
    bool BusinessSummaryIncludeStageSummary,
    bool BusinessSummaryIncludeGoals
);

public record UpdateNotificationPreferenceRequest(
    string? DailyDigestTime,
    int? DigestFrequencyDays,
    int? DigestDaysOfWeek,
    int? SaleFollowUpDays,
    bool? DigestEnabled,
    bool? StageChangeNotificationsEnabled,
    bool? SaleNotificationsEnabled,
    int? NewClientNotificationDaysAfter = null,
    string? NewClientNotificationTime = null,
    bool? EmailOnFriendRequests = null,
    bool? EmailOnGeneralNotifications = null,
    bool? BusinessSummaryEnabled = null,
    int? BusinessSummaryFrequency = null,
    int? BusinessSummaryDayOfWeek = null,
    bool? BusinessSummaryIncludeCounts = null,
    bool? BusinessSummaryIncludeStageSummary = null,
    bool? BusinessSummaryIncludeGoals = null
);
