namespace OnTimeCRM.Application.DTOs.Friends;

// ── Friend list ───────────────────────────────────────────────────────────────
public record FriendDto(
    Guid FriendshipId,
    Guid UserId,
    string FullName,
    string? AvatarUrl
);

// ── Pending request ───────────────────────────────────────────────────────────
public record FriendRequestDto(
    Guid FriendshipId,
    Guid SenderId,
    string SenderName,
    string SenderEmail,
    DateTimeOffset SentAt
);

// ── Public KPI profile (fields gated by the friend's privacy settings) ────────
public record FriendProfileDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    int? SalesCount,
    int? ProposalsCount,
    decimal? ConversionRate,
    int? HotDealsCount,
    decimal? AvgSaleValue
);

// ── Requests ──────────────────────────────────────────────────────────────────
public record SendFriendRequestDto(string Email);

// ── Public profile settings ──────────────────────────────────────────────────
public record PublicProfileSettingsDto(
    bool ShowSalesCount,
    bool ShowConversionRate,
    bool ShowProposalsCount,
    bool ShowHotDealsCount,
    bool ShowAvgSaleValue,
    string? AvatarUrl
);
