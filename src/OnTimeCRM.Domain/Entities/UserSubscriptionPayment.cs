using OnTimeCRM.Domain.Common;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Domain.Entities;

public class UserSubscriptionPayment : BaseEntity
{
    public Guid UserId { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public SubscriptionPaymentStatus Status { get; set; } = SubscriptionPaymentStatus.Pending;
    public PaymentProvider Provider { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    // Stripe
    public string? StripePaymentIntentId { get; set; }
    public string? StripeInvoiceId { get; set; }

    // Ifthenpay
    public string? IfthenpayReference { get; set; }
    public string? IfthenpayMBWayAlias { get; set; }
    public string? IfthenpayTransactionId { get; set; }

    public string? FailureReason { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
