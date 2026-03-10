using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

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
            p.SaleFollowUpDays,
            p.DigestEnabled,
            p.StageChangeNotificationsEnabled,
            p.SaleNotificationsEnabled,
            p.NewClientNotificationDaysAfter,
            p.NewClientNotificationTime);
}
