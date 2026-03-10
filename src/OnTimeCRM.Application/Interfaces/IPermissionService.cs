using OnTimeCRM.Application.DTOs.Permissions;

namespace OnTimeCRM.Application.Interfaces;

public interface IPermissionService
{
    Task<IEnumerable<MenuPermissionDto>> GetPermissionsAsync(int role, CancellationToken ct = default);
    Task UpdatePermissionsAsync(int role, IEnumerable<UpdateMenuPermissionRequest> updates, CancellationToken ct = default);
}
