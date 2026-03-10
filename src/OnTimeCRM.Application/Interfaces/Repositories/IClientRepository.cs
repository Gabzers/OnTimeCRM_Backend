using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IClientRepository
{
    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Paginated, filtered list via PostgreSQL function fn_get_clients_paged. Pass brandId for Manager role.</summary>
    Task<PagedResult<ClientListDto>> GetPagedAsync(Guid userId, Guid? brandId, ClientFilterParams filter, CancellationToken ct = default);

    /// <summary>Load raw entity (no navigation) — for ownership checks and soft-delete.</summary>
    Task<Client?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load active client (is_active = true) — throws nothing, returns null if absent.</summary>
    Task<Client?> FindActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load client with CurrentStage and active Proposals→Vehicles→Model (for stage-change flow).</summary>
    Task<Client?> FindWithStageAndProposalsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Hot deals: active clients with Hot temperature in non-final stage.</summary>
    Task<IEnumerable<ClientListDto>> GetHotDealsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Stage history audit trail for a client.</summary>
    Task<IEnumerable<StageHistoryDto>> GetHistoryAsync(Guid clientId, CancellationToken ct = default);

    /// <summary>Past sales attached to a specific client.</summary>
    Task<IEnumerable<ClientSaleHistoryDto>> GetSalesHistoryAsync(Guid clientId, CancellationToken ct = default);

    // ── Writes (EF tracking — commit with IUnitOfWork.SaveChangesAsync) ─────

    void Add(Client client);
    void AddProposal(Proposal proposal);
    void AddProposalVehicle(ProposalVehicle vehicle);
    void AddHistory(ClientStageHistory history);
    void AddNotification(Notification notification);
}
