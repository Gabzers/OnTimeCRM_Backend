using OnTime.Application.DTOs.Brands;
using OnTime.Domain.Entities;

namespace OnTime.Application.Interfaces.Repositories;

public interface IBrandRepository
{
    Task<IEnumerable<BrandListDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<Brand?> FindAsync(Guid id, CancellationToken ct = default);
    Task<BrandDto?> GetDtoByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    void Add(Brand brand);

    // ── Vehicle brands the Stand sells (see USER-BRANDS.md) ────────────────
    Task<IEnumerable<Guid>> GetVehicleBrandIdsAsync(Guid brandId, CancellationToken ct = default);
    Task SetVehicleBrandIdsAsync(Guid brandId, IEnumerable<Guid> vehicleBrandIds, CancellationToken ct = default);

    // ── Membership grants ────────────────────────────────────────────────────
    Task GrantMembershipAsync(Guid brandId, Guid userId, CancellationToken ct = default);
    Task RevokeMembershipAsync(Guid brandId, Guid userId, CancellationToken ct = default);
}
