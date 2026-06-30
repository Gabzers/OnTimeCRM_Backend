using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Vehicles;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _db;

    public VehicleRepository(AppDbContext db) => _db = db;

    // ── Reads ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<VehicleBrandDto>> GetBrandsAsync(CancellationToken ct = default) =>
        await _db.VehicleBrands
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new VehicleBrandDto(b.Id, b.Name, b.LogoUrl, b.Models.Count()))
            .ToListAsync(ct);

    public async Task<PagedResult<VehicleModelListDto>> GetModelsAsync(
        VehicleSearchParams p,
        Guid userId,
        CancellationToken ct = default)
    {
        // A model only shows up while its VehicleBrand is currently configured for the calling
        // user's ACTIVE FILIAL (BrandVehicleBrand, Manager/Admin-managed) — not a per-user
        // selection anymore. Unconfiguring a brand on the Stand hides its models for every user
        // of that Stand without deleting anything (see USER-BRANDS.md). Independent of the
        // model's own manual IsActive toggle (red/green status dot).
        var allowedBrandIds = await GetAllowedVehicleBrandIdsAsync(userId, ct);
        await EnsureClonedForBrandsAsync(userId, allowedBrandIds, ct);

        var query = _db.UserVehicleModels
            .AsNoTracking()
            .Include(m => m.VehicleBrand)
            .Where(m => m.UserId == userId && allowedBrandIds.Contains(m.VehicleBrandId))
            .AsQueryable();

        if (p.BrandId.HasValue)
            query = query.Where(m => m.VehicleBrandId == p.BrandId.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(s) ||
                m.VehicleBrand.Name.ToLower().Contains(s));
        }

        if (p.Configured.HasValue)
        {
            var isConfigured = p.Configured.Value;
            query = query.Where(m =>
                m.Versions.Any(v => v.ExternalColors != null && v.ExternalColors != "" && v.ExternalColors != "[]") == isConfigured);
        }

        var total = await query.CountAsync(ct);
        var size  = Math.Clamp(p.PageSize, 1, 50);

        var items = await query
            .OrderBy(m => m.VehicleBrand.Name).ThenBy(m => m.Name)
            .Skip((p.Page - 1) * size)
            .Take(size)
            .Select(m => new VehicleModelListDto(
                m.Id, m.VehicleBrandId, m.VehicleBrand.Name, m.Name,
                m.Version, m.Year, m.FuelType == null ? null : (int)m.FuelType,
                m.IsActive,
                m.Versions.Any(v => v.ExternalColors != null && v.ExternalColors != "" && v.ExternalColors != "[]")))
            .ToListAsync(ct);

        return new PagedResult<VehicleModelListDto>(items, total, p.Page, size);
    }

    public async Task<VehicleModelDto?> GetModelDtoAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var m = await _db.UserVehicleModels
            .AsNoTracking()
            .Include(x => x.VehicleBrand)
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

        return m is null ? null : ToModelDto(m);
    }

    public async Task<VehicleBrand?> FindBrandAsync(Guid id, CancellationToken ct = default) =>
        await _db.VehicleBrands.FindAsync(new object[] { id }, ct);

    public async Task<UserVehicleModel?> FindModelAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.UserVehicleModels
            .Include(m => m.VehicleBrand)
            .Include(m => m.Versions)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);

    public async Task<bool> IsModelInUseAsync(Guid modelId, CancellationToken ct = default) =>
        await _db.ProposalVehicles.AnyAsync(v => v.ModelId == modelId, ct) ||
        await _db.Sales.AnyAsync(s => s.ModelId == modelId, ct);

    public async Task<bool> IsVersionInUseAsync(Guid versionId, CancellationToken ct = default) =>
        await _db.ProposalVehicles.AnyAsync(v => v.VersionId == versionId, ct);

    public async Task<bool> IsBrandAllowedForUserAsync(Guid userId, Guid vehicleBrandId, CancellationToken ct = default)
    {
        var allowed = await GetAllowedVehicleBrandIdsAsync(userId, ct);
        return allowed.Contains(vehicleBrandId);
    }

    // ── Stand-level vehicle brand config + lazy clone (see USER-BRANDS.md) ─

    private async Task<List<Guid>> GetAllowedVehicleBrandIdsAsync(Guid userId, CancellationToken ct)
    {
        var brandId = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.BrandId)
            .FirstOrDefaultAsync(ct);

        if (brandId is null) return [];

        return await _db.BrandVehicleBrands
            .Where(bvb => bvb.BrandId == brandId.Value)
            .Select(bvb => bvb.VehicleBrandId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// The first time a user's Stand allows a VehicleBrand they haven't personally cloned yet,
    /// clone its global models/versions into their own catalog — same idempotent clone as before,
    /// just triggered by the Stand's config instead of a per-user "select brand" action.
    /// </summary>
    private async Task EnsureClonedForBrandsAsync(Guid userId, IEnumerable<Guid> vehicleBrandIds, CancellationToken ct)
    {
        var alreadyCloned = await _db.UserVehicleModels
            .Where(m => m.UserId == userId)
            .Select(m => m.VehicleBrandId)
            .Distinct()
            .ToListAsync(ct);

        var notYetCloned = vehicleBrandIds.Except(alreadyCloned).ToList();
        if (notYetCloned.Count == 0) return;

        foreach (var brandId in notYetCloned)
            await CloneFromGlobalAsync(userId, brandId, ct);
    }

    private async Task CloneFromGlobalAsync(Guid userId, Guid brandId, CancellationToken ct)
    {
        var globalModels = await _db.VehicleModels
            .Include(m => m.Versions)
            .Where(m => m.BrandId == brandId)
            .ToListAsync(ct);

        foreach (var gm in globalModels)
        {
            var clone = new UserVehicleModel
            {
                UserId         = userId,
                VehicleBrandId = brandId,
                Name           = gm.Name,
                Version        = gm.Version,
                Year           = gm.Year,
                FuelType       = gm.FuelType,
                BasePrice      = gm.BasePrice,
                ImageUrl       = gm.ImageUrl,
            };
            foreach (var gv in gm.Versions)
            {
                clone.Versions.Add(new UserVehicleVersion
                {
                    Name           = gv.Name,
                    ExternalColors = gv.ExternalColors,
                    InternalColors = gv.InternalColors,
                });
            }
            _db.UserVehicleModels.Add(clone);
        }

        if (globalModels.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    // ── Version Reads ──────────────────────────────────────────────────────

    public async Task<IEnumerable<VehicleVersionDto>> GetVersionsAsync(Guid modelId, Guid userId, CancellationToken ct = default) =>
        await _db.UserVehicleVersions
            .AsNoTracking()
            .Where(v => v.ModelId == modelId && v.Model.UserId == userId)
            .OrderBy(v => v.Name)
            .Select(v => new VehicleVersionDto(
                v.Id, v.Name,
                ColorArrayHelper.Parse(v.ExternalColors),
                ColorArrayHelper.Parse(v.InternalColors)))
            .ToListAsync(ct);

    public async Task<UserVehicleVersion?> FindVersionAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.UserVehicleVersions
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == id && v.Model.UserId == userId, ct);

    // ── Writes ────────────────────────────────────────────────────────────

    public void AddBrand(VehicleBrand brand) => _db.VehicleBrands.Add(brand);
    public void RemoveBrand(VehicleBrand brand) => _db.VehicleBrands.Remove(brand);
    public void AddModel(UserVehicleModel model) => _db.UserVehicleModels.Add(model);
    public void RemoveModel(UserVehicleModel model) => _db.UserVehicleModels.Remove(model);
    public void AddVersion(UserVehicleVersion version) => _db.UserVehicleVersions.Add(version);
    public void RemoveVersion(UserVehicleVersion version) => _db.UserVehicleVersions.Remove(version);

    // ── Mapper ────────────────────────────────────────────────────────────

    private static VehicleModelDto ToModelDto(UserVehicleModel m) =>
        new(m.Id, m.VehicleBrandId, m.VehicleBrand.Name, m.Name, m.Version, m.Year,
            m.FuelType is null ? null : (int)m.FuelType,
            m.BasePrice, m.ImageUrl,
            m.Versions
                .OrderBy(v => v.Name)
                .Select(v => new VehicleVersionDto(
                    v.Id, v.Name,
                    ColorArrayHelper.Parse(v.ExternalColors),
                    ColorArrayHelper.Parse(v.InternalColors)))
                .ToList(),
            m.IsActive);
}
