using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Infrastructure.Persistence;

namespace OnTimeCRM.Infrastructure.Repositories;

public sealed class StageRepository : IStageRepository
{
    private readonly AppDbContext _db;

    public StageRepository(AppDbContext db) => _db = db;

    // ── Reads ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ClientStageDto>> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var stages = await _db.ClientStages
            .AsNoTracking()
            .Include(s => s.Templates)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);

        return stages.Select(ToDto);
    }

    public async Task<ClientStage?> FindAsync(Guid id, CancellationToken ct = default) =>
        await _db.ClientStages.FindAsync(new object[] { id }, ct);

    public async Task<ClientStage?> FindWithTemplatesAsync(Guid id, CancellationToken ct = default) =>
        await _db.ClientStages
            .Include(s => s.Templates)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ClientStage?> FindFirstByUserAsync(Guid userId, CancellationToken ct = default) =>
        await _db.ClientStages
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderBy(s => s.Order)
            .FirstOrDefaultAsync(ct);

    public async Task<ClientStage?> FindByOrderAsync(Guid userId, int order, CancellationToken ct = default) =>
        await _db.ClientStages
            .Where(s => s.UserId == userId && s.IsActive && s.Order == order)
            .FirstOrDefaultAsync(ct);

    public async Task<ClientStage?> FindWonStageAsync(Guid userId, CancellationToken ct = default) =>
        await _db.ClientStages
            .Include(s => s.Templates.Where(t => t.IsEnabled))
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsWon, ct);

    public async Task<ClientStage?> FindLostStageAsync(Guid userId, CancellationToken ct = default) =>
        await _db.ClientStages
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsLost, ct);

    public async Task<bool> HasClientsAsync(Guid stageId, CancellationToken ct = default) =>
        await _db.Clients.AnyAsync(c => c.CurrentStageId == stageId, ct);

    public async Task<int> GetMaxOrderAsync(Guid userId, CancellationToken ct = default) =>
        await _db.ClientStages
            .Where(s => s.UserId == userId)
            .MaxAsync(s => (int?)s.Order, ct) ?? -1;

    public async Task<IEnumerable<ClientStage>> GetByIdsAsync(
        Guid userId,
        IEnumerable<Guid> ids,
        CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _db.ClientStages
            .Where(s => s.UserId == userId && idList.Contains(s.Id))
            .ToListAsync(ct);
    }

    public async Task<StageNotificationTemplate?> FindTemplateAsync(
        Guid stageId,
        Guid templateId,
        CancellationToken ct = default) =>
        await _db.StageNotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.StageId == stageId, ct);

    // ── Writes ────────────────────────────────────────────────────────────

    public void Add(ClientStage stage) => _db.ClientStages.Add(stage);
    public void Remove(ClientStage stage) => _db.ClientStages.Remove(stage);
    public void AddTemplate(StageNotificationTemplate template) => _db.StageNotificationTemplates.Add(template);
    public void RemoveTemplate(StageNotificationTemplate template) => _db.StageNotificationTemplates.Remove(template);

    // ── Mapper ────────────────────────────────────────────────────────────

    private static ClientStageDto ToDto(ClientStage s) =>
        new(s.Id, s.Name, s.Color, s.Order, s.IsFinal, s.IsWon, s.IsLost, s.IsActive,
            (s.Templates ?? Enumerable.Empty<StageNotificationTemplate>()).Select(ToTemplateDto));

    private static StageTemplateDto ToTemplateDto(StageNotificationTemplate t) =>
        new(t.Id, t.Title, t.DaysAfter, t.IsEnabled, t.TimeOfDay, t.OverridesNewClientNotification);
}
