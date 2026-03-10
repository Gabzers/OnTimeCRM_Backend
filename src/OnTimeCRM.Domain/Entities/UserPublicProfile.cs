using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

/// <summary>
/// Controls which KPIs a user exposes publicly to friends.
/// Created automatically on user registration with all flags set to false.
/// </summary>
public class UserPublicProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? AvatarUrl { get; set; }
    public bool ShowSalesCount { get; set; } = false;
    public bool ShowConversionRate { get; set; } = false;
    public bool ShowProposalsCount { get; set; } = false;
    public bool ShowHotDealsCount { get; set; } = false;
    public bool ShowAvgSaleValue { get; set; } = false;

    // Navigation
    public User User { get; set; } = null!;
}
