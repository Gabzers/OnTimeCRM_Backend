using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface INotificationPreferenceRepository
{
    Task<NotificationPreferenceDto?> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationPreference?> FindByUserAsync(Guid userId, CancellationToken ct = default);
}
