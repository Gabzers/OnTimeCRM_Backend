using System.Text.Json;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Sales;
using OnTime.Application.Interfaces;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;

namespace OnTime.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _clientRepo;
    private readonly IStageRepository  _stageRepo;
    private readonly IUserRepository   _userRepo;
    private readonly IUnitOfWork       _uow;

    public ClientService(
        IClientRepository clientRepo,
        IStageRepository stageRepo,
        IUserRepository userRepo,
        IUnitOfWork uow)
    {
        _clientRepo = clientRepo;
        _stageRepo  = stageRepo;
        _userRepo   = userRepo;
        _uow        = uow;
    }

    // ── Paged list via PG function ────────────────────────────────────────
    public Task<PagedResult<ClientListDto>> GetPagedAsync(
        Guid userId, Guid? brandId, ClientFilterParams filter, CancellationToken ct = default) =>
        _clientRepo.GetPagedAsync(userId, brandId, filter, ct);

    // ── Detail ────────────────────────────────────────────────────────────
    public async Task<ClientDto> GetByIdAsync(
        Guid id, Guid userId, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindActiveAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        return ToDto(client);
    }

    // ── Create (ATOMIC: client + first proposal) ──────────────────────────
    public async Task<ClientDto> CreateAsync(
        Guid userId, CreateClientRequest req, CancellationToken ct = default)
    {
        var initialStage = req.StageId.HasValue
            ? await ResolveStageAsync(userId, req.StageId.Value, ct)
            : await ResolveDefaultStageAsync(userId, ct);

        var p = req.Proposal;
        var vehicleList = p?.Vehicles?.ToList();

        if (p is not null && (vehicleList is null || vehicleList.Count == 0)
            && await _userRepo.IsAutomotiveAsync(userId, ct))
            throw new ApiException(ApiErrorCatalog.PROPOSAL_MISSING_VEHICLE);

        var proposalValue = vehicleList?.Any(v => v.Price.HasValue) == true
            ? (decimal?)vehicleList.Sum(v => v.Price ?? 0m)
            : p?.ProposalValue;

        var client = new Client
        {
            UserId            = userId,
            FullName          = req.FullName,
            Email             = req.Email,
            Phone             = req.Phone,
            TaxId             = req.TaxId,
            LeadSource        = req.LeadSource ?? 0,
            CurrentStageId    = initialStage.Id,
            Temperature       = DealTemperature.Hot,
            LastInteractionAt = DateTimeOffset.UtcNow
        };

        _clientRepo.Add(client);

        if (req.HasProposal)
        {
            var proposal = new Proposal
            {
                Client                = client,
                UserId                = userId,
                Status                = ProposalStatus.Active,
                BusinessType          = (BusinessType)(p?.BusinessType ?? 0),
                PaymentType           = (PaymentType)(p?.PaymentType ?? 0),
                ProposalValue         = proposalValue,
                Discount              = p?.Discount,
                ProposalDate          = p?.ProposalDate ?? DateTimeOffset.UtcNow,
                HasTradeIn            = p?.HasTradeIn ?? false,
                TradeInPlate          = p?.TradeIn?.Plate,
                TradeInBrand          = p?.TradeIn?.Brand,
                TradeInModel          = p?.TradeIn?.Model,
                TradeInYear           = p?.TradeIn?.Year,
                TradeInKm             = p?.TradeIn?.Km,
                TradeInEstimatedValue = p?.TradeIn?.EstimatedValue,
                TradeInType           = p?.TradeIn?.Type is null ? null : (TradeInType)p.TradeIn.Type
            };

            _clientRepo.AddProposal(proposal);

            if (vehicleList is not null)
            {
                foreach (var v in vehicleList)
                {
                    _clientRepo.AddProposalVehicle(new ProposalVehicle
                    {
                        Proposal      = proposal,
                        ModelId       = v.ModelId,
                        FreeTextModel = v.FreeTextModel,
                        IsPreferred   = v.IsPreferred,
                        Price         = v.Price,
                        Discount      = v.Discount,
                        VersionId     = v.VersionId,
                        ExternalColor = v.ExternalColor,
                        InternalColor = v.InternalColor,
                        Plate         = v.Plate
                    });
                }
            }
        }

        _clientRepo.AddHistory(new ClientStageHistory
        {
            Client      = client,
            UserId      = userId,
            FromStageId = null,
            ToStageId   = initialStage.Id
        });

        await _uow.SaveChangesAsync(ct);

        client.CurrentStage = initialStage;
        return ToDto(client);
    }

    // ── Stage change (main flow — single transaction) ─────────────────────
    public async Task<ClientDto> UpdateStageAsync(
        Guid id, Guid userId, UpdateClientStageRequest req, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindWithStageAndProposalsAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        var newStage = await _stageRepo.FindWithTemplatesAsync(req.StageId, ct);
        if (newStage is null || newStage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        var activeProposal = client.Proposals
            .FirstOrDefault(p => p.Status == ProposalStatus.Active);

        var history = new ClientStageHistory
        {
            ClientId         = client.Id,
            UserId           = userId,
            FromStageId      = client.CurrentStageId,
            ToStageId        = newStage.Id,
            Obs              = req.Obs,
            ProposalSnapshot = BuildSnapshot(activeProposal)
        };
        _clientRepo.AddHistory(history);

        // Any recurring-notification series from the client's previous stage visit stops
        // unconditionally on stage change, regardless of occurrence count or Done state.
        await _clientRepo.DeactivateActiveNotificationSeriesAsync(client.Id, ct);

        client.CurrentStageId    = newStage.Id;
        client.LastInteractionAt = DateTimeOffset.UtcNow;
        client.CurrentStage      = newStage;

        if (newStage.AffectsTemperature)
        {
            // Apply this stage's immediate (DaysAfterEntry = 0) rule, if any. Later time-based
            // transitions for this stage are applied by the pg_cron job, not here.
            var immediateRule = newStage.TemperatureRules?
                .Where(r => r.DaysAfterEntry == 0)
                .OrderByDescending(r => r.DaysAfterEntry)
                .FirstOrDefault();
            if (immediateRule is not null)
                client.Temperature = (DealTemperature)immediateRule.Temperature;
        }
        else if (!newStage.IsFinal)
        {
            client.Temperature = RecalcTemperature(client.LastInteractionAt);
        }

        GenerateFromTemplates(userId, client.Id, activeProposal?.Id, newStage, history);

        await _uow.SaveChangesAsync(ct);
        return ToDto(client);
    }

    // ── Soft delete ───────────────────────────────────────────────────────
    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        client.IsActive = false;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ClientDto> UpdateAsync(
        Guid id, Guid userId, UpdateClientRequest req, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindWithStageAndProposalsAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        if (req.Notes is not null)
            client.Notes = req.Notes;

        await _uow.SaveChangesAsync(ct);
        return ToDto(client);
    }

    // ── Hot deals via PG function ─────────────────────────────────────────
    public Task<IEnumerable<ClientListDto>> GetHotDealsAsync(
        Guid userId, CancellationToken ct = default) =>
        _clientRepo.GetHotDealsAsync(userId, ct);

    // ── Stage history ─────────────────────────────────────────────────────
    public async Task<IEnumerable<StageHistoryDto>> GetHistoryAsync(
        Guid id, Guid userId, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        return await _clientRepo.GetHistoryAsync(id, ct);
    }

    // ── Sales history ─────────────────────────────────────────────────────
    public async Task<IEnumerable<ClientSaleHistoryDto>> GetSalesHistoryAsync(
        Guid id, Guid userId, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        return await _clientRepo.GetSalesHistoryAsync(id, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static DealTemperature RecalcTemperature(DateTimeOffset? lastInteraction)
    {
        if (!lastInteraction.HasValue) return DealTemperature.Cold;

        var hours = (DateTimeOffset.UtcNow - lastInteraction.Value).TotalHours;
        return hours switch
        {
            <= 72  => DealTemperature.Hot,
            <= 240 => DealTemperature.Warm,
            _      => DealTemperature.Cold
        };
    }

    private async Task<ClientStage> ResolveStageAsync(Guid userId, Guid stageId, CancellationToken ct)
    {
        var stage = await _stageRepo.FindWithTemplatesAsync(stageId, ct);
        if (stage is null || stage.UserId != userId)
            throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);

        return stage;
    }

    private async Task<ClientStage> ResolveDefaultStageAsync(Guid userId, CancellationToken ct)
    {
        var stage = await _stageRepo.FindByOrderAsync(userId, 2, ct)
            ?? await _stageRepo.FindFirstByUserAsync(userId, ct);

        if (stage is not null)
            return stage;

        SeedDefaultStages(userId);
        await _uow.SaveChangesAsync(ct);

        return await _stageRepo.FindByOrderAsync(userId, 2, ct)
            ?? await _stageRepo.FindFirstByUserAsync(userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);
    }

    private void SeedDefaultStages(Guid userId)
    {
        var stages = new[]
        {
            new ClientStage { UserId = userId, Name = "Aguarda Agendamento de Visita", Color = "#94A3B8", Order = 0, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { UserId = userId, Name = "Visita Agendada",               Color = "#3B82F6", Order = 1, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { UserId = userId, Name = "Agendar Test Drive",             Color = "#8B5CF6", Order = 2, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { UserId = userId, Name = "Test Drive Marcado",             Color = "#F59E0B", Order = 3, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { UserId = userId, Name = "Aguarda Decisao",               Color = "#EF4444", Order = 4, IsFinal = false, IsWon = false, IsLost = false },
            new ClientStage { UserId = userId, Name = "Venda",                         Color = "#10B981", Order = 5, IsFinal = true,  IsWon = true,  IsLost = false },
            new ClientStage { UserId = userId, Name = "Perdido",                       Color = "#6B7280", Order = 6, IsFinal = true,  IsWon = false, IsLost = true  },
        };

        foreach (var stage in stages)
            _stageRepo.Add(stage);

        stages[1].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[1], UserId = userId, Title = "Confirmar visita", DaysAfter = 1 }
        };
        stages[4].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[4], UserId = userId, Title = "Ligar ao cliente", DaysAfter = 2 }
        };
        stages[5].Templates = new List<StageNotificationTemplate>
        {
            new() { Stage = stages[5], UserId = userId, Title = "Contacto pos-venda", DaysAfter = 30 }
        };
    }

    private static string? BuildSnapshot(Proposal? p)
    {
        if (p is null) return null;

        var vehicles = p.Vehicles?.Select(v => new
        {
            mid  = v.ModelId,
            name = v.Model?.Name ?? v.FreeTextModel,
            pref = v.IsPreferred
        }).ToArray();

        var obj = new
        {
            pid     = p.Id,
            pd      = p.ProposalDate,
            bt      = (int)p.BusinessType,
            pt      = (int)p.PaymentType,
            val     = p.ProposalValue,
            disc    = p.Discount,
            tradeIn = p.HasTradeIn,
            ti      = p.HasTradeIn ? new
            {
                plate = p.TradeInPlate,
                brand = p.TradeInBrand,
                model = p.TradeInModel,
                year  = p.TradeInYear,
                km    = p.TradeInKm,
                est   = p.TradeInEstimatedValue
            } : null,
            vehicles
        };

        return JsonSerializer.Serialize(obj);
    }

    private void GenerateFromTemplates(
        Guid userId, Guid clientId, Guid? proposalId, ClientStage stage, ClientStageHistory history)
    {
        if (stage.Templates is null) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var t in stage.Templates.Where(t => t.IsEnabled))
        {
            _clientRepo.AddNotification(new Notification
            {
                UserId       = userId,
                ClientId     = clientId,
                ProposalId   = proposalId,
                Trigger      = NotificationTrigger.StageChanged,
                Status       = NotificationStatus.Pending,
                Title        = t.Title,
                ScheduledFor = now.AddDays(t.DaysAfter)
            });

            // Recurring templates also get a series row tracking this stage visit's progress —
            // the first occurrence above already counts as #1; the pg_cron job generates #2+.
            if (t.IsRecurring)
            {
                _clientRepo.AddNotificationSeries(new ClientStageNotificationSeries
                {
                    ClientStageHistoryId = history.Id,
                    TemplateId           = t.Id,
                    OccurrenceCount       = 1,
                    IsActive              = true,
                    LastFiredAt           = now
                });
            }
        }
    }

    private static ClientListDto ToListDto(Client c) =>
        new(c.Id, c.FullName, c.Phone, c.Email,
            c.LeadSource, (int)c.Temperature,
            c.CurrentStageId, c.CurrentStage?.Name ?? string.Empty,
            c.CurrentStage?.Color,
            c.CurrentStage?.IsFinal ?? false, c.CurrentStage?.IsWon ?? false, c.CurrentStage?.IsLost ?? false,
            c.LastInteractionAt, c.CreatedAt);

    private static ClientDto ToDto(Client c) =>
        new(c.Id, c.FullName, c.Email, c.Phone, c.TaxId,
            c.LeadSource,
            c.CurrentStageId,
            c.CurrentStage?.Name    ?? string.Empty,
            c.CurrentStage?.Color,
            c.CurrentStage?.IsFinal ?? false,
            c.CurrentStage?.IsWon   ?? false,
            c.CurrentStage?.IsLost  ?? false,
            (int)c.Temperature,
            c.LastInteractionAt,
            c.CreatedAt,
            c.UpdatedAt,
            c.Notes);
}
