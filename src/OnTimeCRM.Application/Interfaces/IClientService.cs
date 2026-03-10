using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Sales;

namespace OnTimeCRM.Application.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientListDto>> GetPagedAsync(Guid userId, Guid? brandId, ClientFilterParams filter, CancellationToken ct = default);
    Task<ClientDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ClientDto> CreateAsync(Guid userId, CreateClientRequest request, CancellationToken ct = default);
    Task<ClientDto> UpdateStageAsync(Guid id, Guid userId, UpdateClientStageRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ClientListDto>> GetHotDealsAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<StageHistoryDto>> GetHistoryAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ClientSaleHistoryDto>> GetSalesHistoryAsync(Guid id, Guid userId, CancellationToken ct = default);
}
