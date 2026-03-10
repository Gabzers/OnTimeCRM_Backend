using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;

namespace OnTimeCRM.Application.Services;

public class ClientStageService : IClientStageService
{
    private readonly IStageRepository _repo;
    private readonly IUnitOfWork      _uow;

    public ClientStageService(IStageRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow  = uow;
    }

    public Task<IEnumerable<ClientStageDto>> GetByUserAsync(
        Guid userId, CancellationToken ct = default) =>
        _repo.GetByUserAsync(userId, ct);

    public async Task<ClientStageDto> CreateAsync(
        Guid userId, CreateStageRequest req, CancellationToken ct = default)
    {
        var maxOrder = await _repo.GetMaxOrderAsync(userId, ct);

        var stage = new ClientStage
        {
            UserId  = userId,
            Name    = req.Name,
            Color   = req.Color,
            Order   = maxOrder + 1,
            IsFinal = false,
            IsWon   = false,
            IsLost  = false,
            IsActive = true
        };

        _repo.Add(stage);
        await _uow.SaveChangesAsync(ct);

        return ToDto(stage);
    }

    public async Task<ClientStageDto> UpdateAsync(
        Guid id, Guid userId, UpdateStageRequest req, CancellationToken ct = default)
    {
        var stage = await _repo.FindWithTemplatesAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        stage.Name     = req.Name;
        stage.Color    = req.Color;
        stage.IsActive = req.IsActive;

        await _uow.SaveChangesAsync(ct);
        return ToDto(stage);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        if (await _repo.HasClientsAsync(id, ct))
            throw new ApiException(ApiErrorCatalog.STAGE_HAS_CLIENTS);

        _repo.Remove(stage);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(
        Guid userId, ReorderStagesRequest req, CancellationToken ct = default)
    {
        var items  = req.Items.ToList();
        var stages = await _repo.GetByIdsAsync(userId, items.Select(i => i.StageId), ct);
        var map    = stages.ToDictionary(s => s.Id);

        foreach (var item in items)
            if (map.TryGetValue(item.StageId, out var s))
                s.Order = item.Order;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<StageTemplateDto> AddTemplateAsync(
        Guid stageId, Guid userId, CreateStageTemplateRequest req, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var template = new StageNotificationTemplate
        {
            StageId   = stageId,
            Title     = req.Title,
            DaysAfter = req.DaysAfter,
            IsEnabled = true,
            TimeOfDay = req.TimeOfDay,
            OverridesNewClientNotification = req.OverridesNewClientNotification
        };

        _repo.AddTemplate(template);
        await _uow.SaveChangesAsync(ct);

        return ToTemplateDto(template);
    }

    public async Task<StageTemplateDto> UpdateTemplateAsync(
        Guid stageId, Guid templateId, Guid userId, UpdateStageTemplateRequest req, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var template = await _repo.FindTemplateAsync(stageId, templateId, ct)
            ?? throw new ApiException(ApiErrorCatalog.NOTIFICATION_NOT_FOUND);

        template.Title     = req.Title;
        template.DaysAfter = req.DaysAfter;
        template.IsEnabled = req.IsEnabled;
        template.TimeOfDay = req.TimeOfDay;
        template.OverridesNewClientNotification = req.OverridesNewClientNotification;

        await _uow.SaveChangesAsync(ct);
        return ToTemplateDto(template);
    }

    public async Task DeleteTemplateAsync(
        Guid stageId, Guid templateId, Guid userId, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var template = await _repo.FindTemplateAsync(stageId, templateId, ct)
            ?? throw new ApiException(ApiErrorCatalog.NOTIFICATION_NOT_FOUND);

        _repo.RemoveTemplate(template);
        await _uow.SaveChangesAsync(ct);
    }

    private static ClientStageDto ToDto(ClientStage s) =>
        new(s.Id, s.Name, s.Color, s.Order, s.IsFinal, s.IsWon, s.IsLost, s.IsActive,
            (s.Templates ?? Enumerable.Empty<StageNotificationTemplate>()).Select(ToTemplateDto));

    private static StageTemplateDto ToTemplateDto(StageNotificationTemplate t) =>
        new(t.Id, t.Title, t.DaysAfter, t.IsEnabled, t.TimeOfDay, t.OverridesNewClientNotification);
}
