using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Friends;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

public sealed class FriendshipRepository : IFriendshipRepository
{
    private readonly AppDbContext _db;

    public FriendshipRepository(AppDbContext db) => _db = db;

    public async Task<UserFriendship?> FindAsync(
        Guid senderId, Guid receiverId, CancellationToken ct = default) =>
        await _db.UserFriendships
            .FirstOrDefaultAsync(f =>
                (f.SenderId == senderId && f.ReceiverId == receiverId) ||
                (f.SenderId == receiverId && f.ReceiverId == senderId), ct);

    public async Task<UserFriendship?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.UserFriendships.FindAsync(new object[] { id }, ct);

    public async Task<IEnumerable<FriendDto>> GetAcceptedFriendsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.UserFriendships
            .AsNoTracking()
            .Include(f => f.Sender)
            .Include(f => f.Receiver)
            .Where(f =>
                f.Status == FriendshipStatus.Accepted &&
                (f.SenderId == userId || f.ReceiverId == userId))
            .ToListAsync(ct);

        return rows.Select(f =>
        {
            var friend = f.SenderId == userId ? f.Receiver : f.Sender;
            return new FriendDto(f.Id, friend.Id, friend.FullName, null);
        });
    }

    public async Task<IEnumerable<FriendRequestDto>> GetPendingRequestsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.UserFriendships
            .AsNoTracking()
            .Include(f => f.Sender)
            .Where(f => f.ReceiverId == userId && f.Status == FriendshipStatus.Pending)
            .ToListAsync(ct);

        return rows.Select(f =>
            new FriendRequestDto(f.Id, f.Sender.Id, f.Sender.FullName, f.Sender.Email, f.CreatedAt));
    }

    public async Task<User?> FindUserByEmailAsync(
        string email, CancellationToken ct = default) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

    public async Task<FriendProfileDto?> GetFriendProfileAsync(
        Guid viewerUserId, Guid friendUserId, CancellationToken ct = default)
    {
        // Verify they are actually friends
        var areFriends = await _db.UserFriendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.SenderId == viewerUserId && f.ReceiverId == friendUserId) ||
             (f.SenderId == friendUserId && f.ReceiverId == viewerUserId)), ct);

        if (!areFriends) return null;

        var friend = await _db.Users
            .AsNoTracking()
            .Include(u => u.PublicProfile)
            .FirstOrDefaultAsync(u => u.Id == friendUserId, ct);

        if (friend is null) return null;

        var profile = friend.PublicProfile;

        // Compute KPIs only for the fields marked as public
        int? salesCount      = null;
        int? proposalsCount  = null;
        int? hotDealsCount   = null;
        decimal? avgSaleValue = null;
        decimal? conversionRate = null;

        if (profile is not null)
        {
            if (profile.ShowSalesCount)
                salesCount = await _db.Sales.CountAsync(s => s.UserId == friendUserId, ct);

            if (profile.ShowProposalsCount)
                proposalsCount = await _db.Proposals.CountAsync(p => p.UserId == friendUserId, ct);

            if (profile.ShowHotDealsCount)
                hotDealsCount = await _db.Clients.CountAsync(c =>
                    c.UserId == friendUserId &&
                    c.IsActive &&
                    c.Temperature == DealTemperature.Hot, ct);

            if (profile.ShowAvgSaleValue)
            {
                var avg = await _db.Sales
                    .Where(s => s.UserId == friendUserId)
                    .AverageAsync(s => (decimal?)s.FinalValue, ct);
                avgSaleValue = avg;
            }

            if (profile.ShowConversionRate && proposalsCount.HasValue && salesCount.HasValue &&
                proposalsCount.Value > 0)
            {
                conversionRate = (decimal)salesCount.Value / proposalsCount.Value * 100m;
            }
        }

        return new FriendProfileDto(
            friend.Id,
            friend.FullName,
            profile?.AvatarUrl,
            salesCount,
            proposalsCount,
            conversionRate,
            hotDealsCount,
            avgSaleValue);
    }

    public async Task<UserPublicProfile?> FindPublicProfileAsync(
        Guid userId, CancellationToken ct = default) =>
        await _db.UserPublicProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public void Add(UserFriendship friendship)        => _db.UserFriendships.Add(friendship);
    public void Remove(UserFriendship friendship)     => _db.UserFriendships.Remove(friendship);
    public void AddPublicProfile(UserPublicProfile p) => _db.UserPublicProfiles.Add(p);
}
