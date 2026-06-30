using System.ComponentModel.DataAnnotations;
using OnTime.Application.Common;

namespace OnTime.Application.DTOs.Auth;

public record RegisterManagerRequest(
    [Required] string FullName,
    [Required][EmailAddress] string Email,
    [Required][StrongPassword] string Password,
    string? Phone = null,
    /// <summary>Optional. If provided, manager is associated with this company.</summary>
    Guid? CompanyId = null,
    /// <summary>Optional. If provided, manager is associated with this brand.</summary>
    Guid? BrandId = null,
    /// <summary>Used only when CompanyId is null: creates a new company with this name.</summary>
    string? CompanyName = null,
    /// <summary>Used only when BrandId is null and CompanyId is null: creates a new brand.</summary>
    string? BrandName = null,
    string? BrandColor = null,
    /// <summary>Used only when creating a new Stand — whether it sells vehicles. See ROADMAP.md.</summary>
    bool IsAutomotive = true
);

public record RegisterSalespersonRequest(
    [Required] string FullName,
    [Required][EmailAddress] string Email,
    [Required][StrongPassword] string Password,
    string? Phone = null,
    /// <summary>Optional. If provided user is associated with this company.</summary>
    Guid? CompanyId = null,
    /// <summary>Optional. If provided user is associated with this brand.</summary>
    Guid? BrandId = null
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record LoginResponseDto(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string FullName,
    int Role,
    Guid? CompanyId,
    string? CompanyName,
    Guid? BrandId,
    string? BrandName,
    string? BrandColor,
    int AccountStatus,
    int SubscriptionStatus,
    DateTimeOffset? SubscriptionExpiresAt,
    /// <summary>False hides vehicle-related UI for this user's active Stand. Defaults true when
    /// the user has no brand yet. See ROADMAP.md "Not an automotive account" toggle.</summary>
    bool IsAutomotive = true
);

// ── Public company/brand lookup (for registration dropdowns) ──────────────────
public record CompanyLookupDto(Guid Id, string Name);
public record BrandLookupDto(Guid Id, string Name, string? PrimaryColor);

// ── Multi-Stand membership (see USER-BRANDS.md / 04-DECISIONS) ───────────────
public record MembershipDto(Guid CompanyId, string CompanyName, Guid BrandId, string BrandName);
public record SwitchBrandRequest([Required] Guid BrandId);

