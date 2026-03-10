using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

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

    // ── Today's notifications via PG function ─────────────────────────────
    public async Task<IEnumerable<NotificationDto>> GetTodayAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var rows = await _db.Database
            .SqlQuery<TodayNotificationRow>($"SELECT * FROM fn_get_today_notifications({userId})")
            .ToListAsync(ct);

        return rows.Select(r => new NotificationDto(
            r.Id, r.ClientId, r.ClientName, r.ProposalId, r.SaleId,
            r.Trigger, r.Status, r.Title!, r.Body,
            r.ScheduledFor, r.DoneAt, r.SnoozedUntil, r.CreatedAt));
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

    // ── Private result type ───────────────────────────────────────────────
    private record TodayNotificationRow(
        Guid Id, Guid? ClientId, string? ClientName,
        Guid? ProposalId, Guid? SaleId,
        int Trigger, int Status,
        string? Title, string? Body,
        DateTimeOffset ScheduledFor,
        DateTimeOffset? DoneAt, DateTimeOffset? SnoozedUntil,
        DateTimeOffset CreatedAt);
}
