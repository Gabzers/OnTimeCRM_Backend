using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;

namespace OnTimeCRM.Application.Services;

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

        await _uow.SaveChangesAsync(ct);

        return new NotificationPreferenceDto(
            pref.DailyDigestTime.ToString("HH:mm"),
            pref.DigestFrequencyDays,
            pref.SaleFollowUpDays,
            pref.DigestEnabled,
            pref.StageChangeNotificationsEnabled,
            pref.SaleNotificationsEnabled,
            pref.NewClientNotificationDaysAfter,
            pref.NewClientNotificationTime);
    }
}
