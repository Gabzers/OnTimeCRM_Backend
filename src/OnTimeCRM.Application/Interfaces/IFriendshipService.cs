using OnTimeCRM.Application.DTOs.Friends;

namespace OnTimeCRM.Application.Interfaces;

public interface IFriendshipService
{
    Task<FriendRequestDto> SendRequestAsync(Guid senderId, SendFriendRequestDto dto, CancellationToken ct = default);
    Task<FriendDto> AcceptAsync(Guid receiverId, Guid requestId, CancellationToken ct = default);
    Task RejectAsync(Guid receiverId, Guid requestId, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, Guid friendUserId, CancellationToken ct = default);
    Task<IEnumerable<FriendDto>> GetFriendsAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<FriendRequestDto>> GetPendingRequestsAsync(Guid userId, CancellationToken ct = default);
    Task<FriendProfileDto> GetFriendProfileAsync(Guid viewerUserId, Guid friendUserId, CancellationToken ct = default);
    Task<PublicProfileSettingsDto> GetMyPublicProfileAsync(Guid userId, CancellationToken ct = default);
    Task<PublicProfileSettingsDto> UpdateMyPublicProfileAsync(Guid userId, PublicProfileSettingsDto dto, CancellationToken ct = default);
}
