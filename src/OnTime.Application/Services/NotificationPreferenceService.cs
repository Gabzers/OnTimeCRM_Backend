using OnTime.Application.Common;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Enums;

namespace OnTime.Application.Services;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _repo;
    private readonly IUnitOfWork                       _uow;

    public NotificationPreferenceService(
        INotificationPreferenceRepository repo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public async Task<NotificationPreferenceDto> GetAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _repo.GetByUserAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);
    }

    public async Task<NotificationPreferenceDto> UpdateAsync(
        Guid userId,
        UpdateNotificationPreferenceRequest req,
        CancellationToken ct = default)
    {
        var pref = await _repo.FindByUserAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (req.DigestFrequencyDays.HasValue)
            pref.DigestFrequencyDays = req.DigestFrequencyDays.Value;

        if (req.DigestDaysOfWeek.HasValue)
            pref.DigestDaysOfWeek = req.DigestDaysOfWeek.Value;

        if (req.SaleFollowUpDays.HasValue)
            pref.SaleFollowUpDays = req.SaleFollowUpDays.Value;

        if (req.DigestEnabled.HasValue)
            pref.DigestEnabled = req.DigestEnabled.Value;

        if (req.StageChangeNotificationsEnabled.HasValue)
            pref.StageChangeNotificationsEnabled = req.StageChangeNotificationsEnabled.Value;

        if (req.SaleNotificationsEnabled.HasValue)
            pref.SaleNotificationsEnabled = req.SaleNotificationsEnabled.Value;

        if (req.NewClientNotificationDaysAfter.HasValue)
            pref.NewClientNotificationDaysAfter = req.NewClientNotificationDaysAfter.Value;

        if (!string.IsNullOrWhiteSpace(req.NewClientNotificationTime))
            pref.NewClientNotificationTime = req.NewClientNotificationTime;

        if (!string.IsNullOrWhiteSpace(req.DailyDigestTime) &&
            TimeOnly.TryParse(req.DailyDigestTime, out var t))
        {
            pref.DailyDigestTime = t;
        }

        if (req.EmailOnFriendRequests.HasValue)
            pref.EmailOnFriendRequests = req.EmailOnFriendRequests.Value;

        if (req.EmailOnGeneralNotifications.HasValue)
            pref.EmailOnGeneralNotifications = req.EmailOnGeneralNotifications.Value;

        if (req.BusinessSummaryEnabled.HasValue)
            pref.BusinessSummaryEnabled = req.BusinessSummaryEnabled.Value;

        if (req.BusinessSummaryFrequency.HasValue)
            pref.BusinessSummaryFrequency = (SummaryFrequency)req.BusinessSummaryFrequency.Value;

        if (req.BusinessSummaryDayOfWeek.HasValue)
            pref.BusinessSummaryDayOfWeek = req.BusinessSummaryDayOfWeek.Value;

        if (req.BusinessSummaryIncludeCounts.HasValue)
            pref.BusinessSummaryIncludeCounts = req.BusinessSummaryIncludeCounts.Value;

        if (req.BusinessSummaryIncludeStageSummary.HasValue)
            pref.BusinessSummaryIncludeStageSummary = req.BusinessSummaryIncludeStageSummary.Value;

        if (req.BusinessSummaryIncludeGoals.HasValue)
            pref.BusinessSummaryIncludeGoals = req.BusinessSummaryIncludeGoals.Value;

        await _uow.SaveChangesAsync(ct);

        return new NotificationPreferenceDto(
            pref.DailyDigestTime.ToString("HH:mm"),
            pref.DigestFrequencyDays,
            pref.DigestDaysOfWeek,
            pref.SaleFollowUpDays,
            pref.DigestEnabled,
            pref.StageChangeNotificationsEnabled,
            pref.SaleNotificationsEnabled,
            pref.NewClientNotificationDaysAfter,
            pref.NewClientNotificationTime,
            pref.EmailOnFriendRequests,
            pref.EmailOnGeneralNotifications,
            pref.BusinessSummaryEnabled,
            (int)pref.BusinessSummaryFrequency,
            pref.BusinessSummaryDayOfWeek,
            pref.BusinessSummaryIncludeCounts,
            pref.BusinessSummaryIncludeStageSummary,
            pref.BusinessSummaryIncludeGoals);
    }
}
