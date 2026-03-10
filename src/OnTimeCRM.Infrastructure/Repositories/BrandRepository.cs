using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

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
            return new BrandListDto(b.Id, b.Name, b.PrimaryColor, b.IsActive, count);
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

    private static BrandDto ToDto(Brand b) =>
        new(b.Id, b.CompanyId, b.Name,
            b.Description, b.Phone, b.Email, b.Address, b.LogoUrl,
            b.PrimaryColor, b.IsActive, b.CreatedAt);
}
