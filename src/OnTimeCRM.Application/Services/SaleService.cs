using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;

namespace OnTimeCRM.Application.Services;

public class SaleService : ISaleService
{
    private readonly ISaleRepository _repo;

    public SaleService(ISaleRepository repo) => _repo = repo;

    public Task<PagedResult<SaleListDto>> GetPagedAsync(
        Guid userId, SaleFilterParams filter, CancellationToken ct = default) =>
        _repo.GetPagedAsync(userId, filter, ct);

    public async Task<SaleDto> GetByIdAsync(
        Guid id, Guid userId, CancellationToken ct = default)
    {
        // Repository filters by both id and userId — not found means either missing or wrong user
        var dto = await _repo.GetDtoByIdAsync(id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.SALE_NOT_FOUND);
        return dto;
    }

    public Task<DashboardDto> GetDashboardAsync(
        Guid userId, CancellationToken ct = default) =>
        _repo.GetDashboardAsync(userId, ct);
}
