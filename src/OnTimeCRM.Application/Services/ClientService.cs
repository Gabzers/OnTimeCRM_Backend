using System.Text.Json;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _clientRepo;
    private readonly IStageRepository  _stageRepo;
    private readonly IUnitOfWork       _uow;

    public ClientService(
        IClientRepository clientRepo,
        IStageRepository stageRepo,
        IUnitOfWork uow)
    {
        _clientRepo = clientRepo;
        _stageRepo  = stageRepo;
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
        ClientStage? initialStage = null;

        if (req.StageId.HasValue)
        {
            initialStage = await _stageRepo.FindWithTemplatesAsync(req.StageId.Value, ct);
            if (initialStage is null || initialStage.UserId != userId)
                throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);
        }
        else
        {
            // Default to Order=2 ("Agendar Test Drive"); fall back to first active stage
            initialStage = await _stageRepo.FindByOrderAsync(userId, 2, ct)
                        ?? await _stageRepo.FindFirstByUserAsync(userId, ct)
                        ?? throw new ApiException(ApiErrorCatalog.STAGE_NOT_FOUND);
        }

        var p = req.Proposal;
        var vehicleList = p?.Vehicles?.ToList();
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
            LeadSource        = (LeadSource)(req.LeadSource ?? 0),
            CurrentStageId    = initialStage.Id,
            Temperature       = DealTemperature.Hot,
            LastInteractionAt = DateTimeOffset.UtcNow
        };

        _clientRepo.Add(client);

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
                    InternalColor = v.InternalColor
                });
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

        _clientRepo.AddHistory(new ClientStageHistory
        {
            ClientId         = client.Id,
            UserId           = userId,
            FromStageId      = client.CurrentStageId,
            ToStageId        = newStage.Id,
            Obs              = req.Obs,
            ProposalSnapshot = BuildSnapshot(activeProposal)
        });

        client.CurrentStageId    = newStage.Id;
        client.LastInteractionAt = DateTimeOffset.UtcNow;
        client.CurrentStage      = newStage;

        if (!newStage.IsFinal)
            client.Temperature = RecalcTemperature(client.LastInteractionAt);

        GenerateFromTemplates(userId, client.Id, activeProposal?.Id, newStage);

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
        Guid userId, Guid clientId, Guid? proposalId, ClientStage stage)
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
        }
    }

    private static ClientListDto ToListDto(Client c) =>
        new(c.Id, c.FullName, c.Phone, c.Email,
            (int)c.LeadSource, (int)c.Temperature,
            c.CurrentStageId, c.CurrentStage?.Name ?? string.Empty,
            c.CurrentStage?.Color, c.LastInteractionAt, c.CreatedAt);

    private static ClientDto ToDto(Client c) =>
        new(c.Id, c.FullName, c.Email, c.Phone, c.TaxId,
            (int)c.LeadSource,
            c.CurrentStageId,
            c.CurrentStage?.Name    ?? string.Empty,
            c.CurrentStage?.Color,
            c.CurrentStage?.IsFinal ?? false,
            c.CurrentStage?.IsWon   ?? false,
            c.CurrentStage?.IsLost  ?? false,
            (int)c.Temperature,
            c.LastInteractionAt,
            c.CreatedAt,
            c.UpdatedAt);
}
