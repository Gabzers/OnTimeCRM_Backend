using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public ClientRepository(AppDbContext db) => _db = db;

    // ── Paginated list ──────────────────────────────────────────────────────
    public async Task<PagedResult<ClientListDto>> GetPagedAsync(
        Guid userId,
        Guid? brandId,
        ClientFilterParams filter,
        CancellationToken ct = default)
    {
        var page = Math.Max(filter.Page, 1);
        var size = Math.Clamp(filter.PageSize, 1, 50);

        var rows = await _db.Database
            .SqlQuery<ClientPagedRow>(
                $"SELECT * FROM fn_get_clients_paged({userId}, {brandId}, {filter.StageId}, {filter.Temperature}, {filter.LeadSource}, {filter.Search}, {page}, {size})")
            .ToListAsync(ct);

        var total = rows.FirstOrDefault()?.TotalCount ?? 0;
        var items = rows.Select(r => new ClientListDto(
            r.Id, r.FullName, r.Phone, r.Email,
            r.LeadSource, r.Temperature,
            r.CurrentStageId, r.StageName, r.StageColor,
            r.LastInteractionAt, r.CreatedAt))
            .ToList();

        return new PagedResult<ClientListDto>(items, (int)total, page, size);
    }

    // ── Find ─────────────────────────────────────────────────────────────────
    public Task<Client?> FindAsync(Guid id, CancellationToken ct = default) =>
        _db.Clients.FindAsync(new object[] { id }, ct).AsTask();

    public Task<Client?> FindActiveAsync(Guid id, CancellationToken ct = default) =>
        _db.Clients
            .Include(c => c.CurrentStage)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

    public Task<Client?> FindWithStageAndProposalsAsync(Guid id, CancellationToken ct = default) =>
        _db.Clients
            .Include(c => c.CurrentStage)
                .ThenInclude(s => s.Templates.Where(t => t.IsEnabled))
            .Include(c => c.Proposals.Where(p => p.Status == ProposalStatus.Active))
                .ThenInclude(p => p.Vehicles)
                    .ThenInclude(v => v.Model)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

    // ── Hot deals — PostgreSQL fn_get_hot_deals ───────────────────────────
    public async Task<IEnumerable<ClientListDto>> GetHotDealsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        const int limit = 10;
        var rows = await _db.Database
            .SqlQuery<HotDealRow>($"SELECT * FROM fn_get_hot_deals({userId}, {limit})")
            .ToListAsync(ct);

        return rows.Select(r => new ClientListDto(
            r.Id, r.FullName, r.Phone, r.Email,
            r.LeadSource, r.Temperature,
            r.CurrentStageId, r.StageName, r.StageColor,
            r.LastInteractionAt, r.CreatedAt));
    }

    // ── History & sales history ───────────────────────────────────────────
    public async Task<IEnumerable<StageHistoryDto>> GetHistoryAsync(
        Guid clientId,
        CancellationToken ct = default) =>
        await _db.ClientStageHistories
            .AsNoTracking()
            .Include(h => h.FromStage)
            .Include(h => h.ToStage)
            .Where(h => h.ClientId == clientId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new StageHistoryDto(
                h.Id,
                h.FromStageId,
                h.FromStage != null ? h.FromStage.Name : null,
                h.ToStageId,
                h.ToStage.Name,
                h.ToStage.Color,
                h.Obs,
                h.ProposalSnapshot,
                h.CreatedAt))
            .ToListAsync(ct);

    public async Task<IEnumerable<ClientSaleHistoryDto>> GetSalesHistoryAsync(
        Guid clientId,
        CancellationToken ct = default) =>
        await _db.Sales
            .AsNoTracking()
            .Include(s => s.Model)
            .Where(s => s.ClientId == clientId)
            .OrderByDescending(s => s.SoldAt)
            .Select(s => new ClientSaleHistoryDto(
                s.Id,
                s.Model != null ? s.Model.Name : null,
                s.FreeTextModel,
                s.FinalValue,
                (int)s.PaymentType,
                s.SoldAt))
            .ToListAsync(ct);

    // ── Writes ────────────────────────────────────────────────────────────
    public void Add(Client client) => _db.Clients.Add(client);
    public void AddProposal(Proposal proposal) => _db.Proposals.Add(proposal);
    public void AddProposalVehicle(ProposalVehicle pv) => _db.ProposalVehicles.Add(pv);
    public void AddHistory(ClientStageHistory history) => _db.ClientStageHistories.Add(history);
    public void AddNotification(Notification notification) => _db.Notifications.Add(notification);

    // ── Private result types for PG function mapping ──────────────────────
    private record ClientPagedRow(
        Guid Id, string FullName, string? Phone, string? Email,
        int LeadSource, int Temperature,
        Guid CurrentStageId, string StageName, string? StageColor,
        DateTimeOffset? LastInteractionAt, DateTimeOffset CreatedAt,
        long TotalCount);

    private record HotDealRow(
        Guid Id, string FullName, string? Phone, string? Email,
        int LeadSource, int Temperature,
        Guid CurrentStageId, string StageName, string? StageColor,
        DateTimeOffset? LastInteractionAt, DateTimeOffset CreatedAt);
}
