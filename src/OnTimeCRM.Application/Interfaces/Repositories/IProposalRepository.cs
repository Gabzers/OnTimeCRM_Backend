using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Interfaces.Repositories;

public interface IProposalRepository
{
    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Paginated, filtered list via PostgreSQL function fn_get_proposals_paged.</summary>
    Task<PagedResult<ProposalListDto>> GetPagedAsync(Guid userId, ProposalFilterParams filter, CancellationToken ct = default);

    /// <summary>Full proposal DTO (with vehicles, trade-in, etc.) — null if not found.</summary>
    Task<ProposalDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load raw entity — for ownership checks.</summary>
    Task<Proposal?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load proposal including its ProposalVehicles — for update/replace.</summary>
    Task<Proposal?> FindWithVehiclesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load proposal with Client→CurrentStage navigation — for mark-lost / convert.</summary>
    Task<Proposal?> FindWithClientAndStageAsync(Guid id, CancellationToken ct = default);

    // ── Writes ───────────────────────────────────────────────────────────────

    void Add(Proposal proposal);
    void AddVehicles(IEnumerable<ProposalVehicle> vehicles);
    void RemoveVehicle(ProposalVehicle vehicle);
}
