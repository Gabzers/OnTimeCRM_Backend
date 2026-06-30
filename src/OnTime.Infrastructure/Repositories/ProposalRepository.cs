using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Proposals;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class ProposalRepository : IProposalRepository
{
    private readonly AppDbContext _db;

    public ProposalRepository(AppDbContext db) => _db = db;

    // ── Paginated list ──────────────────────────────────────────────────────
    public async Task<PagedResult<ProposalListDto>> GetPagedAsync(
        Guid userId,
        ProposalFilterParams filter,
        CancellationToken ct = default)
    {
        var page = Math.Max(filter.Page, 1);
        var size = Math.Clamp(filter.PageSize, 1, 50);

        var query = _db.Proposals
            .AsNoTracking()
            .Include(p => p.Client)
            .Where(p => p.UserId == userId);

        if (filter.Status.HasValue)
            query = query.Where(p => (int)p.Status == filter.Status.Value);
        if (filter.BusinessType.HasValue)
            query = query.Where(p => (int)p.BusinessType == filter.BusinessType.Value);
        if (filter.PaymentType.HasValue)
            query = query.Where(p => (int)p.PaymentType == filter.PaymentType.Value);
        if (filter.DateFrom.HasValue)
            query = query.Where(p => p.ProposalDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(p => p.ProposalDate <= filter.DateTo.Value);
        if (filter.StageId.HasValue)
            query = query.Where(p => p.Client.CurrentStageId == filter.StageId.Value);
        if (filter.ClientId.HasValue)
            query = query.Where(p => p.ClientId == filter.ClientId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(p => p.Client.FullName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);

        // Vehicle name: the preferred vehicle's "Brand Model" if it has a catalog model, else its
        // free-text model — mirrors the old fn_get_proposals_paged's two-step COALESCE exactly.
        var items = await query
            .OrderByDescending(p => p.ProposalDate ?? p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new ProposalListDto(
                p.Id, p.ClientId, p.Client.FullName,
                (int)p.Status, (int)p.BusinessType, (int)p.PaymentType,
                p.ProposalValue, p.ProposalDate, p.CreatedAt,
                _db.ProposalVehicles
                    .Where(v => v.ProposalId == p.Id && v.ModelId != null)
                    .OrderByDescending(v => v.IsPreferred)
                    .Select(v => v.Model!.VehicleBrand.Name + " " + v.Model.Name)
                    .FirstOrDefault()
                ?? _db.ProposalVehicles
                    .Where(v => v.ProposalId == p.Id && v.FreeTextModel != null)
                    .OrderByDescending(v => v.IsPreferred)
                    .Select(v => v.FreeTextModel)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<ProposalListDto>(items, total, page, size);
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
            .Include(p => p.Vehicles)
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
                    .ThenInclude(m => m!.VehicleBrand)
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
            v.Model?.VehicleBrand?.Name,
            v.FreeTextModel,
            v.IsPreferred,
            v.Price,
            v.Discount,
            v.VersionId,
            v.Version?.Name,
            v.ExternalColor,
            v.InternalColor,
            v.Plate)),
        p.CreatedAt,
        p.UpdatedAt,
        p.Notes);
}
