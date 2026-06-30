using OnTime.Application.Common;
using OnTime.Application.DTOs.Vehicles;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;

namespace OnTime.Application.Services;

public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _repo;
    private readonly IUnitOfWork        _uow;

    public VehicleService(IVehicleRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public Task<IEnumerable<VehicleBrandDto>> GetBrandsAsync(CancellationToken ct = default) =>
        _repo.GetBrandsAsync(ct);

    public Task<PagedResult<VehicleModelListDto>> GetModelsAsync(
        VehicleSearchParams p, Guid userId, CancellationToken ct = default) =>
        _repo.GetModelsAsync(p, userId, ct);

    public async Task<VehicleModelDto> GetModelByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _repo.GetModelDtoAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);
    }

    public async Task<VehicleBrandDto> CreateBrandAsync(
        CreateVehicleBrandRequest req, CancellationToken ct = default)
    {
        var brand = new VehicleBrand { Name = req.Name, LogoUrl = req.LogoUrl };
        _repo.AddBrand(brand);
        await _uow.SaveChangesAsync(ct);
        return new VehicleBrandDto(brand.Id, brand.Name, brand.LogoUrl);
    }

    public async Task DeleteBrandAsync(Guid id, CancellationToken ct = default)
    {
        var brand = await _repo.FindBrandAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_BRAND_NOT_FOUND);
        _repo.RemoveBrand(brand);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<VehicleModelDto> CreateModelAsync(
        CreateVehicleModelRequest req, Guid userId, CancellationToken ct = default)
    {
        var brand = await _repo.FindBrandAsync(req.BrandId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_BRAND_NOT_FOUND);

        // The vehicle brands a user can create models under are configured per-Stand by
        // Manager/Admin (see USER-BRANDS.md) — not picked by the user themselves anymore.
        if (!await _repo.IsBrandAllowedForUserAsync(userId, req.BrandId, ct))
            throw new ApiException(ApiErrorCatalog.VEHICLE_BRAND_NOT_ALLOWED);

        var model = new UserVehicleModel
        {
            UserId         = userId,
            VehicleBrandId = req.BrandId,
            Name           = req.Name,
            Version        = req.Version,
            Year           = req.Year,
            FuelType       = req.FuelType is null ? null : (Domain.Enums.FuelType)req.FuelType,
            BasePrice      = req.BasePrice,
            ImageUrl       = req.ImageUrl
        };

        _repo.AddModel(model);
        await _uow.SaveChangesAsync(ct);

        model.VehicleBrand = brand;
        return new VehicleModelDto(model.Id, model.VehicleBrandId, brand.Name, model.Name,
            model.Version, model.Year,
            model.FuelType is null ? null : (int)model.FuelType,
            model.BasePrice, model.ImageUrl, []);
    }

    public async Task<VehicleModelDto> UpdateModelAsync(
        Guid id, Guid userId, UpdateVehicleModelRequest req, CancellationToken ct = default)
    {
        var model = await _repo.FindModelAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);

        model.Name      = req.Name;
        model.Version   = req.Version;
        model.Year      = req.Year;
        model.FuelType  = req.FuelType is null ? null : (Domain.Enums.FuelType)req.FuelType;
        model.BasePrice = req.BasePrice;
        model.ImageUrl  = req.ImageUrl;

        await _uow.SaveChangesAsync(ct);

        return new VehicleModelDto(model.Id, model.VehicleBrandId, model.VehicleBrand.Name, model.Name,
            model.Version, model.Year,
            model.FuelType is null ? null : (int)model.FuelType,
            model.BasePrice, model.ImageUrl,
            model.Versions
                .OrderBy(v => v.Name)
                .Select(v => new VehicleVersionDto(
                    v.Id, v.Name,
                    ColorArrayHelper.Parse(v.ExternalColors),
                    ColorArrayHelper.Parse(v.InternalColors)))
                .ToList(),
            model.IsActive);
    }

    public async Task SetModelActiveAsync(Guid id, Guid userId, bool isActive, CancellationToken ct = default)
    {
        var model = await _repo.FindModelAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);
        model.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteModelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var model = await _repo.FindModelAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);

        if (await _repo.IsModelInUseAsync(id, ct))
            throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_IN_USE);

        _repo.RemoveModel(model);
        await _uow.SaveChangesAsync(ct);
    }

    // ── Versions ──────────────────────────────────────────────────────────

    public Task<IEnumerable<VehicleVersionDto>> GetVersionsAsync(Guid modelId, Guid userId, CancellationToken ct = default) =>
        _repo.GetVersionsAsync(modelId, userId, ct);

    public async Task<VehicleVersionDto> CreateVersionAsync(
        Guid modelId, Guid userId, CreateVehicleVersionRequest req, CancellationToken ct = default)
    {
        var model = await _repo.FindModelAsync(modelId, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);

        var version = new UserVehicleVersion
        {
            ModelId        = model.Id,
            Name           = req.Name,
            ExternalColors = ColorArrayHelper.Serialize(req.ExternalColors),
            InternalColors = ColorArrayHelper.Serialize(req.InternalColors),
        };
        _repo.AddVersion(version);
        await _uow.SaveChangesAsync(ct);

        return new VehicleVersionDto(version.Id, version.Name,
            ColorArrayHelper.Parse(version.ExternalColors),
            ColorArrayHelper.Parse(version.InternalColors));
    }

    public async Task<VehicleVersionDto> UpdateVersionAsync(
        Guid id, Guid userId, UpdateVehicleVersionRequest req, CancellationToken ct = default)
    {
        var version = await _repo.FindVersionAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);

        version.Name           = req.Name;
        version.ExternalColors = ColorArrayHelper.Serialize(req.ExternalColors);
        version.InternalColors = ColorArrayHelper.Serialize(req.InternalColors);
        await _uow.SaveChangesAsync(ct);

        return new VehicleVersionDto(version.Id, version.Name,
            ColorArrayHelper.Parse(version.ExternalColors),
            ColorArrayHelper.Parse(version.InternalColors));
    }

    public async Task DeleteVersionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var version = await _repo.FindVersionAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_NOT_FOUND);

        if (await _repo.IsVersionInUseAsync(id, ct))
            throw new ApiException(ApiErrorCatalog.VEHICLE_MODEL_IN_USE);

        _repo.RemoveVersion(version);
        await _uow.SaveChangesAsync(ct);
    }
}
