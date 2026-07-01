using OnTime.Domain.Common;
using OnTime.Domain.Enums;

namespace OnTime.Domain.Entities;

public class User : BaseEntity
{
    /// <summary>Optional — users can register without a company/brand.</summary>
    public Guid? CompanyId { get; set; }
    /// <summary>Optional — users can register without a company/brand.</summary>
    public Guid? BrandId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Salesperson;
    public bool IsEmailVerified { get; set; } = false;
    public DateTimeOffset? LastLoginAt { get; set; }
    /// <summary>UI language, e.g. "pt-PT"/"en-US" — drives email template language server-side.</summary>
    public string Locale { get; set; } = "pt-PT";

    // Subscription / account
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.PendingActivation;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Trial;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? SubscriptionStartedAt { get; set; }
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }
    public DateTimeOffset? SubscriptionCancelledAt { get; set; }
    public int GracePeriodDays { get; set; } = 3;

    // Stripe
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public Brand? Brand { get; set; }
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<UserFriendship> SentFriendRequests { get; set; } = new List<UserFriendship>();
    public ICollection<UserFriendship> ReceivedFriendRequests { get; set; } = new List<UserFriendship>();
    public UserPublicProfile? PublicProfile { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public NotificationPreference? NotificationPreference { get; set; }
    public ICollection<UserSubscriptionPayment> SubscriptionPayments { get; set; } = new List<UserSubscriptionPayment>();
    public ICollection<UserBrandMembership> BrandMemberships { get; set; } = new List<UserBrandMembership>();
}
