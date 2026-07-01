using OnTime.Application.DTOs.Stages;
using OnTime.Domain.Entities;

namespace OnTime.Application.Interfaces.Repositories;

public interface IStageRepository
{
    // ── Reads ────────────────────────────────────────────────────────────────

    Task<IEnumerable<ClientStageDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<ClientStage?> FindAsync(Guid id, CancellationToken ct = default);
    Task<ClientStage?> FindWithTemplatesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lowest-order active stage for a user — used on client creation.</summary>
    Task<ClientStage?> FindFirstByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Active stage for a user by its Order value — used when client provides a preferred stage.</summary>
    Task<ClientStage?> FindByOrderAsync(Guid userId, int order, CancellationToken ct = default);

    /// <summary>The IsWon = true stage for a user — used on sale conversion.</summary>
    Task<ClientStage?> FindWonStageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The IsLost = true stage for a user — used on mark-lost.</summary>
    Task<ClientStage?> FindLostStageAsync(Guid userId, CancellationToken ct = default);

    Task<bool> HasClientsAsync(Guid stageId, CancellationToken ct = default);
    Task<int> GetMaxOrderAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Load multiple stages by ID for bulk reorder.</summary>
    Task<IEnumerable<ClientStage>> GetByIdsAsync(Guid userId, IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<StageNotificationTemplate?> FindTemplateAsync(Guid stageId, Guid templateId, CancellationToken ct = default);

    Task<ClientStageTemperatureRule?> FindTemperatureRuleAsync(Guid stageId, Guid ruleId, CancellationToken ct = default);

    // ── Writes ───────────────────────────────────────────────────────────────

    void Add(ClientStage stage);
    void Remove(ClientStage stage);
    void AddTemplate(StageNotificationTemplate template);
    void RemoveTemplate(StageNotificationTemplate template);
    void AddTemperatureRule(ClientStageTemperatureRule rule);
    void RemoveTemperatureRule(ClientStageTemperatureRule rule);
}
