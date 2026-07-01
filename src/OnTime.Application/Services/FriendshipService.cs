using OnTime.Application.Common;
using OnTime.Application.DTOs.Friends;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;

namespace OnTime.Application.Services;

public class FriendshipService : IFriendshipService
{
    private readonly IFriendshipRepository _repo;
    private readonly IUnitOfWork           _uow;
    private readonly INotificationPreferenceRepository _prefRepo;
    private readonly IEmailSender          _emailSender;

    public FriendshipService(
        IFriendshipRepository repo,
        IUnitOfWork uow,
        INotificationPreferenceRepository prefRepo,
        IEmailSender emailSender)
    {
        _repo        = repo;
        _uow         = uow;
        _prefRepo    = prefRepo;
        _emailSender = emailSender;
    }

    public async Task<FriendRequestDto> SendRequestAsync(
        Guid senderId, SendFriendRequestDto dto, CancellationToken ct = default)
    {
        User? found = dto.UserId.HasValue
            ? await _repo.FindUserByIdAsync(dto.UserId.Value, ct)
            : await _repo.FindUserByEmailAsync(dto.Email?.ToLower() ?? string.Empty, ct);
        var receiver = found ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (receiver.Id == senderId)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        var existing = await _repo.FindAsync(senderId, receiver.Id, ct);
        if (existing is not null)
        {
            // If previously rejected, allow re-send by resetting to pending
            if (existing.Status == FriendshipStatus.Rejected && existing.SenderId == senderId)
            {
                existing.Status = FriendshipStatus.Pending;
                await _uow.SaveChangesAsync(ct);
                return new FriendRequestDto(existing.Id, senderId,
                    existing.Sender?.FullName ?? string.Empty,
                    existing.Sender?.Email ?? string.Empty,
                    existing.CreatedAt);
            }
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);
        }

        var friendship = new UserFriendship
        {
            SenderId   = senderId,
            ReceiverId = receiver.Id,
            Status     = FriendshipStatus.Pending
        };

        _repo.Add(friendship);
        await _uow.SaveChangesAsync(ct);

        var sender = await _repo.FindUserByIdAsync(senderId, ct);
        await NotifyFriendRequestAsync(receiver, sender?.FullName ?? string.Empty, ct);

        return new FriendRequestDto(
            friendship.Id, senderId,
            sender?.FullName ?? string.Empty,
            sender?.Email ?? string.Empty,
            friendship.CreatedAt);
    }

    private async Task NotifyFriendRequestAsync(User receiver, string senderName, CancellationToken ct)
    {
        var pref = await _prefRepo.FindByUserAsync(receiver.Id, ct);
        if (pref is not null && !pref.EmailOnFriendRequests) return;

        var subject = EmailTemplates.FriendRequestSubject(receiver.Locale);
        var html = EmailTemplates.FriendRequestBody(receiver.Locale, receiver.FullName, senderName);

        await _emailSender.SendAsync(receiver.Email, receiver.FullName, subject, html, "FriendRequest", ct);
    }

    public Task<IEnumerable<FriendSearchResultDto>> SearchUsersAsync(
        Guid userId, string query, CancellationToken ct = default) =>
        _repo.SearchUsersAsync(userId, query, ct);

    public Task<IEnumerable<SentFriendRequestDto>> GetSentRequestsAsync(
        Guid userId, CancellationToken ct = default) =>
        _repo.GetSentRequestsAsync(userId, ct);

    public async Task CancelRequestAsync(
        Guid senderId, Guid requestId, CancellationToken ct = default)
    {
        var fr = await _repo.FindByIdAsync(requestId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        // Only the sender can withdraw their own still-pending request.
        if (fr.SenderId != senderId)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        if (fr.Status != FriendshipStatus.Pending)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        _repo.Remove(fr);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<FriendDto> AcceptAsync(
        Guid receiverId, Guid requestId, CancellationToken ct = default)
    {
        var fr = await _repo.FindByIdAsync(requestId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (fr.ReceiverId != receiverId)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        if (fr.Status != FriendshipStatus.Pending)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        fr.Status = FriendshipStatus.Accepted;
        await _uow.SaveChangesAsync(ct);

        return new FriendDto(fr.Id, fr.SenderId, string.Empty, null);
    }

    public async Task RejectAsync(
        Guid receiverId, Guid requestId, CancellationToken ct = default)
    {
        var fr = await _repo.FindByIdAsync(requestId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (fr.ReceiverId != receiverId)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        fr.Status = FriendshipStatus.Rejected;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(
        Guid userId, Guid friendUserId, CancellationToken ct = default)
    {
        var fr = await _repo.FindAsync(userId, friendUserId, ct)
            ?? throw new ApiException(ApiErrorCatalog.USER_NOT_FOUND);

        if (fr.Status != FriendshipStatus.Accepted)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);

        _repo.Remove(fr);
        await _uow.SaveChangesAsync(ct);
    }

    public Task<IEnumerable<FriendDto>> GetFriendsAsync(
        Guid userId, CancellationToken ct = default) =>
        _repo.GetAcceptedFriendsAsync(userId, ct);

    public Task<IEnumerable<FriendRequestDto>> GetPendingRequestsAsync(
        Guid userId, CancellationToken ct = default) =>
        _repo.GetPendingRequestsAsync(userId, ct);

    public async Task<FriendProfileDto> GetFriendProfileAsync(
        Guid viewerUserId, Guid friendUserId, CancellationToken ct = default)
    {
        var profile = await _repo.GetFriendProfileAsync(viewerUserId, friendUserId, ct)
            ?? throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);
        return profile;
    }

    public async Task<PublicProfileSettingsDto> GetMyPublicProfileAsync(
        Guid userId, CancellationToken ct = default)
    {
        var profile = await _repo.FindPublicProfileAsync(userId, ct);
        if (profile is null)
        {
            profile = new UserPublicProfile { UserId = userId };
            _repo.AddPublicProfile(profile);
            await _uow.SaveChangesAsync(ct);
        }

        return ToSettingsDto(profile);
    }

    public async Task<PublicProfileSettingsDto> UpdateMyPublicProfileAsync(
        Guid userId, PublicProfileSettingsDto dto, CancellationToken ct = default)
    {
        var profile = await _repo.FindPublicProfileAsync(userId, ct);
        if (profile is null)
        {
            profile = new UserPublicProfile { UserId = userId };
            _repo.AddPublicProfile(profile);
        }

        profile.ShowSalesCount     = dto.ShowSalesCount;
        profile.ShowConversionRate = dto.ShowConversionRate;
        profile.ShowProposalsCount = dto.ShowProposalsCount;
        profile.ShowHotDealsCount  = dto.ShowHotDealsCount;
        profile.ShowAvgSaleValue   = dto.ShowAvgSaleValue;
        if (dto.AvatarUrl is not null)
            profile.AvatarUrl = dto.AvatarUrl;

        await _uow.SaveChangesAsync(ct);
        return ToSettingsDto(profile);
    }

    private static PublicProfileSettingsDto ToSettingsDto(UserPublicProfile p) =>
        new(p.ShowSalesCount, p.ShowConversionRate, p.ShowProposalsCount,
            p.ShowHotDealsCount, p.ShowAvgSaleValue, p.AvatarUrl);
}
