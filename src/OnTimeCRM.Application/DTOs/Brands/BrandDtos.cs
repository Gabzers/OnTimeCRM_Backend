using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Brands;

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
    DateTimeOffset CreatedAt
);

public record BrandListDto(
    Guid Id,
    string Name,
    string? PrimaryColor,
    bool IsActive,
    int UserCount
);

public record CreateBrandRequest(
    [Required] string Name,
    string? Description,
    string? Phone,
    string? Email,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor
);

public record UpdateBrandRequest(
    [Required] string Name,
    string? Description,
    string? Phone,
    string? Email,
    string? Address,
    string? LogoUrl,
    string? PrimaryColor
);

public record SetBrandActiveRequest(bool IsActive);
