using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Permissions;

/// <summary>Simplified 2-column permission model: Read and Edit.</summary>
public record MenuPermissionDto(
    Guid Id,
    int Role,
    string RouteKey,
    bool CanRead,
    bool CanEdit
);

public record UpdateMenuPermissionRequest(
    [Required] string RouteKey,
    bool CanRead,
    bool CanEdit
);
