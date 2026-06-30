using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Notifications;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    // ── Paginated list ────────────────────────────────────────────────────
    public async Task<PagedResult<NotificationDto>> GetPagedAsync(
        Guid userId,
        NotificationFilterParams filter,
        CancellationToken ct = default)
    {
        var query = _db.Notifications
            .AsNoTracking()
            .Include(n => n.Client)
            .Where(n => n.UserId == userId);

        if (filter.Status.HasValue)
            query = query.Where(n => (int)n.Status == filter.Status.Value);

        var total = await query.CountAsync(ct);
        var size  = Math.Clamp(filter.PageSize, 1, 50);

        var items = await query
            .OrderByDescending(n => n.ScheduledFor)
            .Skip((filter.Page - 1) * size)
            .Take(size)
            .Select(n => ToDto(n))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, total, filter.Page, size);
    }

    // ── Today's notifications (pending + due, includes overdue) ───────────
    public async Task<IEnumerable<NotificationDto>> GetTodayAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Client)
            .Where(n => n.UserId == userId && n.Status == NotificationStatus.Pending && n.ScheduledFor <= now)
            .OrderBy(n => n.ScheduledFor)
            .Select(n => ToDto(n))
            .ToListAsync(ct);
    }

    // ── Overdue count ─────────────────────────────────────────────────────
    public async Task<int> GetOverdueCountAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Notifications
            .Where(n => n.UserId == userId &&
                        n.Status == NotificationStatus.Pending &&
                        n.ScheduledFor < now)
            .CountAsync(ct);
    }

    // ── Single notification by primary key ────────────────────────────────
    public async Task<Notification?> FindAsync(Guid id, CancellationToken ct = default) =>
        await _db.Notifications.FindAsync(new object[] { id }, ct);

    // ── Write ─────────────────────────────────────────────────────────────
    public void Add(Notification notification) => _db.Notifications.Add(notification);

    // ── Mapper ────────────────────────────────────────────────────────────
    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.ClientId, n.Client?.FullName, n.ProposalId, n.SaleId,
            (int)n.Trigger, (int)n.Status,
            n.Title, n.Body,
            n.ScheduledFor, n.DoneAt, n.SnoozedUntil, n.CreatedAt);
}
