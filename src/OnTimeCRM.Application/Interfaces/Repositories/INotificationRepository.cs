using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface INotificationRepository
{
    // ── Reads ────────────────────────────────────────────────────────────────

    Task<PagedResult<NotificationDto>> GetPagedAsync(Guid userId, NotificationFilterParams filter, CancellationToken ct = default);

    /// <summary>All pending notifications due today or overdue — via PostgreSQL function.</summary>
    Task<IEnumerable<NotificationDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);

    Task<int> GetOverdueCountAsync(Guid userId, CancellationToken ct = default);
    Task<Notification?> FindAsync(Guid id, CancellationToken ct = default);

    // ── Writes ───────────────────────────────────────────────────────────────

    void Add(Notification notification);
}
