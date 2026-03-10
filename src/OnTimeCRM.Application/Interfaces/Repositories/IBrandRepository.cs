using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IBrandRepository
{
    Task<IEnumerable<BrandListDto>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<Brand?> FindAsync(Guid id, CancellationToken ct = default);
    Task<BrandDto?> GetDtoByIdAsync(Guid id, Guid companyId, CancellationToken ct = default);

    void Add(Brand brand);
}
