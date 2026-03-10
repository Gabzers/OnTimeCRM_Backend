using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Companies;

public record CompanyAdminDto(
    Guid    Id,
    string  Name,
    string? Phone,
    string? Email,
    string? Address,
    bool    IsActive,
    int     BrandCount,
    int     UserCount,
    DateTimeOffset CreatedAt
);

public record CreateCompanyAdminRequest(
    [Required] string Name,
    string? Phone,
    string? Email,
    string? Address
);

public record UpdateCompanyAdminRequest(
    [Required] string Name,
    string? Phone,
    string? Email,
    string? Address
);
