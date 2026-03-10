using OnTimeCRM.Application.DTOs.Friends;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IFriendshipRepository
{
    Task<UserFriendship?> FindAsync(Guid senderId, Guid receiverId, CancellationToken ct = default);
    Task<UserFriendship?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<FriendDto>> GetAcceptedFriendsAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<FriendRequestDto>> GetPendingRequestsAsync(Guid userId, CancellationToken ct = default);
    Task<User?> FindUserByEmailAsync(string email, CancellationToken ct = default);
    Task<FriendProfileDto?> GetFriendProfileAsync(Guid viewerUserId, Guid friendUserId, CancellationToken ct = default);
    Task<UserPublicProfile?> FindPublicProfileAsync(Guid userId, CancellationToken ct = default);

    void Add(UserFriendship friendship);
    void Remove(UserFriendship friendship);
    void AddPublicProfile(UserPublicProfile profile);
}
