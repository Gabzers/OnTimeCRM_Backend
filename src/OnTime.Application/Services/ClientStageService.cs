using OnTime.Application.Common;
using OnTime.Application.DTOs.Stages;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;

namespace OnTime.Application.Services;

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
        if (req.IsWon && req.IsLost)
            throw new ApiException(ApiErrorCatalog.STAGE_WON_AND_LOST);

        var maxOrder = await _repo.GetMaxOrderAsync(userId, ct);

        var stage = new ClientStage
        {
            UserId  = userId,
            Name    = req.Name,
            Color   = req.Color,
            Order   = maxOrder + 1,
            IsFinal = req.IsFinal || req.IsWon || req.IsLost,
            IsWon   = req.IsWon,
            IsLost  = req.IsLost,
            IsActive = true,
            AffectsTemperature = req.AffectsTemperature,
            NotificationsEnabled = req.NotificationsEnabled
        };

        _repo.Add(stage);

        // Only one stage can be the Won stage, and only one the Lost stage — demote the
        // previous holder, mirroring UpdateAsync's behaviour.
        if (req.IsWon)
        {
            var previousWon = await _repo.FindWonStageAsync(userId, ct);
            if (previousWon is not null)
                previousWon.IsWon = false;
        }

        if (req.IsLost)
        {
            var previousLost = await _repo.FindLostStageAsync(userId, ct);
            if (previousLost is not null)
                previousLost.IsLost = false;
        }

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

        if (req.IsWon && req.IsLost)
            throw new ApiException(ApiErrorCatalog.STAGE_WON_AND_LOST);

        stage.Name     = req.Name;
        stage.Color    = req.Color;
        stage.IsActive = req.IsActive;
        stage.IsWon    = req.IsWon;
        stage.IsLost   = req.IsLost;
        stage.IsFinal  = req.IsFinal || req.IsWon || req.IsLost;
        stage.AffectsTemperature = req.AffectsTemperature;
        stage.NotificationsEnabled = req.NotificationsEnabled;

        // Only one stage can be the Won stage, and only one the Lost stage — demote the
        // previous holder so ConvertToSale/MarkLost always have a single, unambiguous target.
        if (req.IsWon)
        {
            var previousWon = await _repo.FindWonStageAsync(userId, ct);
            if (previousWon is not null && previousWon.Id != stage.Id)
                previousWon.IsWon = false;
        }

        if (req.IsLost)
        {
            var previousLost = await _repo.FindLostStageAsync(userId, ct);
            if (previousLost is not null && previousLost.Id != stage.Id)
                previousLost.IsLost = false;
        }

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

        if (req.FixedDayOfWeek.HasValue && req.FixedDayOfMonth.HasValue)
            throw new ApiException(ApiErrorCatalog.RECURRENCE_FIXED_DAY_CONFLICT);

        var template = new StageNotificationTemplate
        {
            StageId   = stageId,
            Title     = req.Title,
            DaysAfter = req.DaysAfter,
            IsEnabled = true,
            TimeOfDay = req.TimeOfDay,
            OverridesNewClientNotification = req.OverridesNewClientNotification,
            IsRecurring = req.IsRecurring,
            RecurrenceIntervalDays = req.RecurrenceIntervalDays,
            FixedDayOfWeek = req.FixedDayOfWeek,
            FixedDayOfMonth = req.FixedDayOfMonth,
            MaxOccurrences = req.MaxOccurrences,
            SendEmail = req.SendEmail
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

        if (req.FixedDayOfWeek.HasValue && req.FixedDayOfMonth.HasValue)
            throw new ApiException(ApiErrorCatalog.RECURRENCE_FIXED_DAY_CONFLICT);

        template.Title     = req.Title;
        template.DaysAfter = req.DaysAfter;
        template.IsEnabled = req.IsEnabled;
        template.TimeOfDay = req.TimeOfDay;
        template.OverridesNewClientNotification = req.OverridesNewClientNotification;
        template.IsRecurring = req.IsRecurring;
        template.RecurrenceIntervalDays = req.RecurrenceIntervalDays;
        template.FixedDayOfWeek = req.FixedDayOfWeek;
        template.FixedDayOfMonth = req.FixedDayOfMonth;
        template.MaxOccurrences = req.MaxOccurrences;
        template.SendEmail = req.SendEmail;

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

    public async Task<TemperatureRuleDto> AddTemperatureRuleAsync(
        Guid stageId, Guid userId, CreateTemperatureRuleRequest req, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var rule = new ClientStageTemperatureRule
        {
            StageId = stageId,
            DaysAfterEntry = req.DaysAfterEntry,
            Temperature = req.Temperature
        };

        _repo.AddTemperatureRule(rule);
        await _uow.SaveChangesAsync(ct);

        return ToTemperatureRuleDto(rule);
    }

    public async Task<TemperatureRuleDto> UpdateTemperatureRuleAsync(
        Guid stageId, Guid ruleId, Guid userId, UpdateTemperatureRuleRequest req, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var rule = await _repo.FindTemperatureRuleAsync(stageId, ruleId, ct)
            ?? throw new ApiException(ApiErrorCatalog.TEMPERATURE_RULE_NOT_FOUND);

        rule.DaysAfterEntry = req.DaysAfterEntry;
        rule.Temperature = req.Temperature;

        await _uow.SaveChangesAsync(ct);
        return ToTemperatureRuleDto(rule);
    }

    public async Task DeleteTemperatureRuleAsync(
        Guid stageId, Guid ruleId, Guid userId, CancellationToken ct = default)
    {
        var stage = await _repo.FindAsync(stageId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        if (stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_WRONG_USER);

        var rule = await _repo.FindTemperatureRuleAsync(stageId, ruleId, ct)
            ?? throw new ApiException(ApiErrorCatalog.TEMPERATURE_RULE_NOT_FOUND);

        _repo.RemoveTemperatureRule(rule);
        await _uow.SaveChangesAsync(ct);
    }

    private static ClientStageDto ToDto(ClientStage s) =>
        new(s.Id, s.Name, s.Color, s.Order, s.IsFinal, s.IsWon, s.IsLost, s.IsActive,
            s.AffectsTemperature, s.NotificationsEnabled,
            (s.Templates ?? Enumerable.Empty<StageNotificationTemplate>()).Select(ToTemplateDto),
            (s.TemperatureRules ?? Enumerable.Empty<ClientStageTemperatureRule>())
                .OrderBy(r => r.DaysAfterEntry).Select(ToTemperatureRuleDto));

    private static StageTemplateDto ToTemplateDto(StageNotificationTemplate t) =>
        new(t.Id, t.Title, t.DaysAfter, t.IsEnabled, t.TimeOfDay, t.OverridesNewClientNotification,
            t.IsRecurring, t.RecurrenceIntervalDays, t.FixedDayOfWeek, t.FixedDayOfMonth, t.MaxOccurrences,
            t.SendEmail);

    private static TemperatureRuleDto ToTemperatureRuleDto(ClientStageTemperatureRule r) =>
        new(r.Id, r.DaysAfterEntry, r.Temperature);
}
