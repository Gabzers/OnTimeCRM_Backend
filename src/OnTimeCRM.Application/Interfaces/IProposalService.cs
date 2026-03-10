using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.DTOs.Sales;

namespace OnTimeCRM.Application.Interfaces;

public interface IProposalService
{
    Task<PagedResult<ProposalListDto>> GetPagedAsync(Guid userId, ProposalFilterParams filter, CancellationToken ct = default);
    Task<ProposalDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ProposalDto> CreateForClientAsync(Guid clientId, Guid userId, CreateProposalRequest request, CancellationToken ct = default);
    Task<ProposalDto> UpdateAsync(Guid id, Guid userId, CreateProposalRequest request, CancellationToken ct = default);
    Task<ProposalDto> MarkLostAsync(Guid id, Guid userId, MarkProposalLostRequest request, CancellationToken ct = default);
    Task<SaleDto> ConvertToSaleAsync(Guid id, Guid userId, ConvertToSaleRequest request, CancellationToken ct = default);
}
