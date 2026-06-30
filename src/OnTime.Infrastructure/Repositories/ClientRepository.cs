using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Sales;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

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

        var query = _db.Clients
            .AsNoTracking()
            .Include(c => c.CurrentStage)
            .Where(c => c.IsActive)
            .Where(c => brandId.HasValue
                ? _db.Users.Any(u => u.Id == c.UserId && u.BrandId == brandId.Value && u.IsActive)
                : c.UserId == userId);

        if (filter.StageId.HasValue)
            query = query.Where(c => c.CurrentStageId == filter.StageId.Value);
        if (filter.Temperature.HasValue)
            query = query.Where(c => (int)c.Temperature == filter.Temperature.Value);
        if (filter.LeadSource.HasValue)
            query = query.Where(c => c.LeadSource == filter.LeadSource.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(s) ||
                (c.Phone != null && c.Phone.ToLower().Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.LastInteractionAt ?? c.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => new ClientListDto(
                c.Id, c.FullName, c.Phone, c.Email,
                c.LeadSource, (int)c.Temperature,
                c.CurrentStageId, c.CurrentStage.Name, c.CurrentStage.Color,
                c.CurrentStage.IsFinal, c.CurrentStage.IsWon, c.CurrentStage.IsLost,
                c.LastInteractionAt, c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ClientListDto>(items, total, page, size);
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

    // ── Hot deals ────────────────────────────────────────────────────────────
    public async Task<IEnumerable<ClientListDto>> GetHotDealsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        const int limit = 10;
        const int hot = 0; // DealTemperature.Hot

        return await _db.Clients
            .AsNoTracking()
            .Include(c => c.CurrentStage)
            .Where(c => c.UserId == userId && c.IsActive
                && (int)c.Temperature == hot && !c.CurrentStage.IsFinal)
            .OrderByDescending(c => c.LastInteractionAt)
            .Take(limit)
            .Select(c => new ClientListDto(
                c.Id, c.FullName, c.Phone, c.Email,
                c.LeadSource, (int)c.Temperature,
                c.CurrentStageId, c.CurrentStage.Name, c.CurrentStage.Color,
                false, false, false,
                c.LastInteractionAt, c.CreatedAt))
            .ToListAsync(ct);
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
}
