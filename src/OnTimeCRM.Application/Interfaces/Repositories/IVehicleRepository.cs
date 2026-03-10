using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Vehicles;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IVehicleRepository
{
    Task<IEnumerable<VehicleBrandDto>> GetBrandsAsync(CancellationToken ct = default);
    Task<PagedResult<VehicleModelListDto>> GetModelsAsync(VehicleSearchParams p, CancellationToken ct = default);
    Task<VehicleModelDto?> GetModelDtoAsync(Guid id, CancellationToken ct = default);
    Task<VehicleBrand?> FindBrandAsync(Guid id, CancellationToken ct = default);
    Task<VehicleModel?> FindModelAsync(Guid id, CancellationToken ct = default);

    void AddBrand(VehicleBrand brand);
    void RemoveBrand(VehicleBrand brand);
    void AddModel(VehicleModel model);
    void RemoveModel(VehicleModel model);

    // Versions
    Task<IEnumerable<VehicleVersionDto>> GetVersionsAsync(Guid modelId, CancellationToken ct = default);
    Task<VehicleModelVersion?> FindVersionAsync(Guid id, CancellationToken ct = default);
    void AddVersion(VehicleModelVersion version);
    void RemoveVersion(VehicleModelVersion version);
}
