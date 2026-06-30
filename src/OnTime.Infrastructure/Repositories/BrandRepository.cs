using Microsoft.EntityFrameworkCore;
using OnTime.Application.DTOs.Brands;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class BrandRepository : IBrandRepository
{
    private readonly AppDbContext _db;

    public BrandRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<BrandListDto>> GetByCompanyAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        var brands = await _db.Brands
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        var userCounts = await _db.Users
            .Where(u => u.CompanyId == companyId)
            .GroupBy(u => u.BrandId)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return brands.Select(b =>
        {
            var count = userCounts.FirstOrDefault(x => x.BrandId == b.Id)?.Count ?? 0;
            return new BrandListDto(b.Id, b.Name, b.PrimaryColor, b.IsActive, count, b.IsAutomotive);
        });
    }

    public async Task<Brand?> FindAsync(Guid id, CancellationToken ct = default) =>
        await _db.Brands.FindAsync(new object[] { id }, ct);

    public async Task<BrandDto?> GetDtoByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken ct = default)
    {
        var b = await _db.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct);

        return b is null ? null : ToDto(b);
    }

    public void Add(Brand brand) => _db.Brands.Add(brand);

    // ── Vehicle brands the Stand sells ──────────────────────────────────────

    public async Task<IEnumerable<Guid>> GetVehicleBrandIdsAsync(Guid brandId, CancellationToken ct = default) =>
        await _db.BrandVehicleBrands
            .AsNoTracking()
            .Where(x => x.BrandId == brandId)
            .Select(x => x.VehicleBrandId)
            .ToListAsync(ct);

    public async Task SetVehicleBrandIdsAsync(
        Guid brandId, IEnumerable<Guid> vehicleBrandIds, CancellationToken ct = default)
    {
        var newSet = vehicleBrandIds.Distinct().ToHashSet();
        var existing = await _db.BrandVehicleBrands
            .Where(x => x.BrandId == brandId)
            .ToListAsync(ct);
        var existingSet = existing.Select(x => x.VehicleBrandId).ToHashSet();

        foreach (var vehicleBrandId in newSet.Except(existingSet))
            _db.BrandVehicleBrands.Add(new BrandVehicleBrand { BrandId = brandId, VehicleBrandId = vehicleBrandId });

        // Removing a vehicle brand from the Stand just removes this row — personal catalogs
        // (UserVehicleModel/UserVehicleVersion) stay untouched, they just stop matching
        // VehicleRepository.GetModelsAsync's visibility filter (hidden, not deleted).
        foreach (var rel in existing.Where(x => !newSet.Contains(x.VehicleBrandId)))
            _db.BrandVehicleBrands.Remove(rel);
    }

    // ── Membership grants ────────────────────────────────────────────────────

    public async Task GrantMembershipAsync(Guid brandId, Guid userId, CancellationToken ct = default)
    {
        var exists = await _db.UserBrandMemberships
            .AnyAsync(m => m.BrandId == brandId && m.UserId == userId, ct);
        if (!exists)
            _db.UserBrandMemberships.Add(new UserBrandMembership { BrandId = brandId, UserId = userId });
    }

    public async Task RevokeMembershipAsync(Guid brandId, Guid userId, CancellationToken ct = default)
    {
        var membership = await _db.UserBrandMemberships
            .FirstOrDefaultAsync(m => m.BrandId == brandId && m.UserId == userId, ct);
        if (membership is not null)
            _db.UserBrandMemberships.Remove(membership);
    }

    private static BrandDto ToDto(Brand b) =>
        new(b.Id, b.CompanyId, b.Name,
            b.Description, b.Phone, b.Email, b.Address, b.LogoUrl,
            b.PrimaryColor, b.IsActive, b.CreatedAt, b.IsAutomotive);
}
