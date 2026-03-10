namespace OnTimeCRM.Domain.Enums;

public enum SubscriptionPlan
{
    Trial = 0,
    Monthly = 1,
    Annual = 2
}

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,     // payment failed, in grace period
    Cancelled = 3,
    Expired = 4
}

public enum PaymentProvider
{
    Stripe = 0,      // credit/debit card, international
    Ifthenpay = 1    // MBWay + Multibanco reference, Portugal
}

public enum PaymentMethodType
{
    Card = 0,
    MBWay = 1,
    Multibanco = 2
}

public enum SubscriptionPaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3,
    Expired = 4  // e.g. Multibanco reference expired without payment
}
