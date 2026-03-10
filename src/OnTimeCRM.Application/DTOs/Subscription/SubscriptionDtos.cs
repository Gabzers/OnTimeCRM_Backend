using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Subscription;

public record SubscriptionStatusDto(
    int AccountStatus,
    int Plan,
    int SubscriptionStatus,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? ExpiresAt,
    bool IsTrialActive,
    int? DaysUntilExpiry,
    bool IsExpired,
    bool CanRenew
);

public record SubscriptionPaymentDto(
    Guid Id,
    int Plan,
    int Status,
    int Provider,
    int PaymentMethod,
    decimal Amount,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? ExpiresAt,
    string? IfthenpayReference,
    string? FailureReason,
    DateTimeOffset CreatedAt
);

public record InitiateSubscriptionRequest(
    [Required] int Plan,
    [Required] int PaymentMethod,
    string? MBWayPhone
);

public record InitiateSubscriptionResponseDto(
    Guid PaymentId,
    int PaymentMethod,
    string? StripeClientSecret,
    string? StripePublishableKey,
    string? MBWayPhone,
    string? MultibancoEntity,
    string? MultibancoReference,
    decimal Amount,
    DateTimeOffset? ExpiresAt
);
