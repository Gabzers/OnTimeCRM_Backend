using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Subscription;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Services;

/// <summary>
/// Stub implementation — payment integration (Stripe/IfthenPay) is post-MVP.
/// Returns sensible defaults so the UI can render without 500 errors.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly IAppDbContext _db;

    public SubscriptionService(IAppDbContext db) => _db = db;

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.AccountStatus })
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        var accountStatus = user?.AccountStatus ?? UserAccountStatus.Active;

        return new SubscriptionStatusDto(
            AccountStatus: (int)accountStatus,
            Plan: 1,
            SubscriptionStatus: (int)accountStatus,
            TrialEndsAt: null,
            ExpiresAt: null,
            IsTrialActive: false,
            DaysUntilExpiry: null,
            IsExpired: accountStatus == UserAccountStatus.Expired,
            CanRenew: accountStatus is UserAccountStatus.Expired or UserAccountStatus.Cancelled
        );
    }

    public Task<IEnumerable<SubscriptionPaymentDto>> GetPaymentsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<SubscriptionPaymentDto>());

    public Task<InitiateSubscriptionResponseDto> InitiateAsync(
        Guid userId,
        InitiateSubscriptionRequest request,
        CancellationToken ct = default)
        => throw new ApiException(ApiErrorCatalog.PAYMENT_PENDING);

    public Task<SubscriptionPaymentDto> GetPaymentStatusAsync(Guid paymentId, Guid userId, CancellationToken ct = default)
        => throw new ApiException(ApiErrorCatalog.PAYMENT_NOT_FOUND);

    public Task CancelAsync(Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;
}
