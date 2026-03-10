using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

public sealed class ProposalRepository : IProposalRepository
{
    private readonly AppDbContext _db;

    public ProposalRepository(AppDbContext db) => _db = db;

    // ── Paginated list — PostgreSQL fn_get_proposals_paged ───────────────
    public async Task<PagedResult<ProposalListDto>> GetPagedAsync(
        Guid userId,
        ProposalFilterParams filter,
        CancellationToken ct = default)
    {
        var page = Math.Max(filter.Page, 1);
        var size = Math.Clamp(filter.PageSize, 1, 50);

        var rows = await _db.Database
            .SqlQuery<ProposalPagedRow>(
                $"SELECT * FROM fn_get_proposals_paged({userId}, {filter.Status}, {filter.BusinessType}, {filter.PaymentType}, {filter.DateFrom}, {filter.DateTo}, {filter.StageId}, {filter.Search}, {filter.ClientId}, {page}, {size})")
            .ToListAsync(ct);

        var total = rows.FirstOrDefault()?.TotalCount ?? 0;
        var items = rows.Select(r => new ProposalListDto(
            r.Id, r.ClientId, r.ClientName,
            r.Status, r.BusinessType, r.PaymentType,
            r.ProposalValue, r.ProposalDate, r.CreatedAt, r.VehicleName))
            .ToList();

        return new PagedResult<ProposalListDto>(items, (int)total, page, size);
    }

    // ── Full DTO (with vehicles, trade-in) ────────────────────────────────
    public async Task<ProposalDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await LoadWithVehiclesAsync(id, ct);
        return p is null ? null : ToDto(p);
    }

    // ── Entity lookups ────────────────────────────────────────────────────
    public Task<Proposal?> FindAsync(Guid id, CancellationToken ct = default) =>
        _db.Proposals.FindAsync(new object[] { id }, ct).AsTask();

    public Task<Proposal?> FindWithVehiclesAsync(Guid id, CancellationToken ct = default) =>
        _db.Proposals
            .Include(p => p.Vehicles)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Proposal?> FindWithClientAndStageAsync(Guid id, CancellationToken ct = default) =>
        _db.Proposals
            .Include(p => p.Client)
                .ThenInclude(c => c.CurrentStage)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    // ── Writes ────────────────────────────────────────────────────────────
    public void Add(Proposal proposal) => _db.Proposals.Add(proposal);

    public void AddVehicles(IEnumerable<ProposalVehicle> vehicles)
    {
        foreach (var v in vehicles)
            _db.ProposalVehicles.Add(v);
    }

    public void RemoveVehicle(ProposalVehicle vehicle) =>
        _db.ProposalVehicles.Remove(vehicle);

    // ── Private helpers ───────────────────────────────────────────────────
    private Task<Proposal?> LoadWithVehiclesAsync(Guid id, CancellationToken ct) =>
        _db.Proposals
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Vehicles)
                .ThenInclude(v => v.Model)
                    .ThenInclude(m => m!.Brand)
            .Include(p => p.Vehicles)
                .ThenInclude(v => v.Version)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    private static ProposalDto ToDto(Proposal p) => new(
        p.Id,
        p.ClientId,
        p.Client?.FullName ?? string.Empty,
        (int)p.Status,
        (int)p.BusinessType,
        (int)p.PaymentType,
        p.ProposalValue,
        p.Discount,
        p.ProposalDate,
        p.LossReason.HasValue ? (int?)p.LossReason.Value : null,
        p.LossNotes,
        p.WonAt,
        p.LostAt,
        p.HasTradeIn,
        p.TradeInType.HasValue ? (int?)p.TradeInType.Value : null,
        p.TradeInPlate,
        p.TradeInBrand,
        p.TradeInModel,
        p.TradeInYear,
        p.TradeInKm,
        p.TradeInEstimatedValue,
        (p.Vehicles ?? Enumerable.Empty<ProposalVehicle>()).Select(v => new ProposalVehicleDto(
            v.Id,
            v.ModelId,
            v.Model?.Name,
            v.Model?.Brand?.Name,
            v.FreeTextModel,
            v.IsPreferred,
            v.Price,
            v.Discount,
            v.VersionId,
            v.Version?.Name,
            v.ExternalColor,
            v.InternalColor)),
        p.CreatedAt,
        p.UpdatedAt);

    private record ProposalPagedRow(
        Guid Id, Guid ClientId, string ClientName,
        int Status, int BusinessType, int PaymentType,
        decimal? ProposalValue, DateTimeOffset? ProposalDate,
        DateTimeOffset CreatedAt, string? VehicleName, long TotalCount);
}
