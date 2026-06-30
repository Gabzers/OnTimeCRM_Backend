using System.ComponentModel.DataAnnotations;
using OnTime.Application.Common;

namespace OnTime.Application.DTOs.Users;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    int Role,
    int AccountStatus,
    int SubscriptionStatus,
    Guid? CompanyId,
    string CompanyName,
    Guid? BrandId,
    string BrandName,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt
);

public record UserListDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    int Role,
    int AccountStatus,
    DateTimeOffset CreatedAt
);

public record UpdateUserRequest(
    string? FullName,
    string? Phone,
    [EmailAddress] string? Email = null
);

public record SetUserActiveRequest(bool IsActive);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required][StrongPassword] string NewPassword
);

/// <summary>
/// Row returned by fn_get_user_by_id / fn_find_user_by_email.
/// Contains all fields needed to build a JWT and LoginResponseDto.
/// </summary>
public record UserDetailRow(
    Guid    Id,
    Guid?   CompanyId,
    Guid?   BrandId,
    string  FullName,
    string  Email,
    string  PasswordHash,
    string? Phone,
    int     Role,
    int     AccountStatus,
    bool    IsActive,
    string  CompanyName,
    bool    CompanyIsActive,
    string  BrandName,
    string? BrandColor,
    bool    BrandIsActive
);

public record ManagerRegistrationResult(Guid CompanyId, Guid BrandId, Guid UserId);
