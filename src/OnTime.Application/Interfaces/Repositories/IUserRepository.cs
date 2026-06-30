using OnTime.Application.DTOs.Users;
using OnTime.Domain.Entities;

namespace OnTime.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> FindAsync(Guid id, CancellationToken ct = default);
    Task<User?> FindWithBrandAndCompanyAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserListDto>> GetByBrandAsync(Guid brandId, CancellationToken ct = default);
    Task<User?> FindInBrandAsync(Guid userId, Guid brandId, CancellationToken ct = default);

    /// <summary>True (default) unless the user's currently-active Stand has explicitly opted
    /// out of being an automotive account (see Brand.IsAutomotive / ROADMAP.md).</summary>
    Task<bool> IsAutomotiveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True if another user (any id other than <paramref name="excludingUserId"/>)
    /// already has this email — used when a user changes their own email via PUT /users/me.</summary>
    Task<bool> EmailTakenByAnotherUserAsync(string email, Guid excludingUserId, CancellationToken ct = default);
}
