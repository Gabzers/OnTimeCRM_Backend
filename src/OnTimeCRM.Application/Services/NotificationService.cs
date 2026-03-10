using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork             _uow;

    public NotificationService(INotificationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public Task<PagedResult<NotificationDto>> GetPagedAsync(
        Guid userId,
        NotificationFilterParams filter,
        CancellationToken ct = default) =>
        _repo.GetPagedAsync(userId, filter, ct);

    public Task<IEnumerable<NotificationDto>> GetTodayAsync(
        Guid userId,
        CancellationToken ct = default) =>
        _repo.GetTodayAsync(userId, ct);

    public Task<int> GetOverdueCountAsync(Guid userId, CancellationToken ct = default) =>
        _repo.GetOverdueCountAsync(userId, ct);

    public async Task<NotificationDto> CreateAsync(
        Guid userId,
        CreateNotificationRequest req,
        CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId       = userId,
            ClientId     = req.ClientId,
            ProposalId   = req.ProposalId,
            SaleId       = req.SaleId,
            Trigger      = NotificationTrigger.Manual,
            Status       = NotificationStatus.Pending,
            Title        = req.Title,
            Body         = req.Body,
            ScheduledFor = req.ScheduledFor
        };

        _repo.Add(notification);
        await _uow.SaveChangesAsync(ct);

        return MapToDto(notification);
    }

    public async Task MarkDoneAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var n = await GetOwnedAsync(id, userId, ct);
        n.Status = NotificationStatus.Done;
        n.DoneAt = DateTimeOffset.UtcNow;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task SnoozeAsync(
        Guid id,
        Guid userId,
        SnoozeNotificationRequest req,
        CancellationToken ct = default)
    {
        var n = await GetOwnedAsync(id, userId, ct);
        n.ScheduledFor = req.SnoozedUntil;
        n.SnoozedUntil = req.SnoozedUntil;
        n.Status       = NotificationStatus.Pending;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task IgnoreAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var n = await GetOwnedAsync(id, userId, ct);
        n.Status = NotificationStatus.Ignored;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Notification> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var n = await _repo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.NOTIFICATION_NOT_FOUND);

        if (n.UserId != userId)
            throw new ApiException(ApiErrorCatalog.NOTIFICATION_WRONG_USER);

        return n;
    }

    private static NotificationDto MapToDto(Notification n) =>
        new(n.Id,
            n.ClientId,
            n.Client?.FullName,
            n.ProposalId,
            n.SaleId,
            (int)n.Trigger,
            (int)n.Status,
            n.Title,
            n.Body,
            n.ScheduledFor,
            n.DoneAt,
            n.SnoozedUntil,
            n.CreatedAt);
}
