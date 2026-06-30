using System.ComponentModel.DataAnnotations;

namespace OnTime.Application.DTOs.Brands;

public record BrandDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Description,
    string? Phone,
    string? Email,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor,
    bool IsActive,
    DateTimeOffset CreatedAt,
    bool IsAutomotive = true
);

public record BrandListDto(
    Guid Id,
    string Name,
    string? PrimaryColor,
    bool IsActive,
    int UserCount,
    bool IsAutomotive = true
);

public record CreateBrandRequest(
    [Required] string Name,
    string? Description,
    string? Phone,
    string? Email,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor,
    bool IsAutomotive = true
);

public record UpdateBrandRequest(
    [Required] string Name,
    string? Description,
    string? Phone,
    string? Email,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor,
    bool IsAutomotive = true
);

public record SetBrandActiveRequest(bool IsActive);

// ── Vehicle brands the Stand sells (Manager/Admin configured) ────────────────
public record BrandVehicleBrandsDto(IEnumerable<Guid> VehicleBrandIds);
public record UpdateBrandVehicleBrandsRequest(IEnumerable<Guid> VehicleBrandIds);

// ── Membership grants ───────────────────────────────────────────────────────────
public record GrantMembershipRequest([Required] Guid UserId);
