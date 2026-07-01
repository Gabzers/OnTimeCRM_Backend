using Microsoft.EntityFrameworkCore;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly AppDbContext _db;

    public NotificationPreferenceRepository(AppDbContext db) => _db = db;

    public async Task<NotificationPreferenceDto?> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var p = await _db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        return p is null ? null : ToDto(p);
    }

    public async Task<NotificationPreference?> FindByUserAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await _db.NotificationPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    private static NotificationPreferenceDto ToDto(NotificationPreference p) =>
        new(p.DailyDigestTime.ToString("HH:mm"),
            p.DigestFrequencyDays,
            p.DigestDaysOfWeek,
            p.SaleFollowUpDays,
            p.DigestEnabled,
            p.StageChangeNotificationsEnabled,
            p.SaleNotificationsEnabled,
            p.NewClientNotificationDaysAfter,
            p.NewClientNotificationTime,
            p.EmailOnFriendRequests,
            p.EmailOnGeneralNotifications,
            p.BusinessSummaryEnabled,
            (int)p.BusinessSummaryFrequency,
            p.BusinessSummaryDayOfWeek,
            p.BusinessSummaryIncludeCounts,
            p.BusinessSummaryIncludeStageSummary,
            p.BusinessSummaryIncludeGoals);
}
