using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Services;

public class ProposalService : IProposalService
{
    private readonly IProposalRepository              _proposalRepo;
    private readonly IClientRepository               _clientRepo;
    private readonly IStageRepository                _stageRepo;
    private readonly INotificationPreferenceRepository _prefRepo;
    private readonly ISaleRepository                 _saleRepo;
    private readonly IUnitOfWork                     _uow;

    public ProposalService(
        IProposalRepository proposalRepo,
        IClientRepository clientRepo,
        IStageRepository stageRepo,
        INotificationPreferenceRepository prefRepo,
        ISaleRepository saleRepo,
        IUnitOfWork uow)
    {
        _proposalRepo = proposalRepo;
        _clientRepo   = clientRepo;
        _stageRepo    = stageRepo;
        _prefRepo     = prefRepo;
        _saleRepo     = saleRepo;
        _uow          = uow;
    }

    // ── Paged list via PG function ────────────────────────────────────────
    public Task<PagedResult<ProposalListDto>> GetPagedAsync(
        Guid userId, ProposalFilterParams filter, CancellationToken ct = default) =>
        _proposalRepo.GetPagedAsync(userId, filter, ct);

    // ── Detail ────────────────────────────────────────────────────────────
    public async Task<ProposalDto> GetByIdAsync(
        Guid id, Guid userId, CancellationToken ct = default)
    {
        var p = await _proposalRepo.FindAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);

        if (p.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        return await _proposalRepo.GetDtoByIdAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);
    }

    // ── Create for existing client ────────────────────────────────────────
    public async Task<ProposalDto> CreateForClientAsync(
        Guid clientId, Guid userId, CreateProposalRequest req, CancellationToken ct = default)
    {
        var client = await _clientRepo.FindAsync(clientId, ct)
            ?? throw new ApiException(ApiErrorCatalog.CLIENT_NOT_FOUND);

        if (client.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        var vehicleList = req.Vehicles?.ToList();
        var proposal = BuildProposal(userId, clientId, req);
        _proposalRepo.Add(proposal);
        AddVehicles(proposal, vehicleList);
        await _uow.SaveChangesAsync(ct);

        return await _proposalRepo.GetDtoByIdAsync(proposal.Id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);
    }

    // ── Update ────────────────────────────────────────────────────────────
    public async Task<ProposalDto> UpdateAsync(
        Guid id, Guid userId, CreateProposalRequest req, CancellationToken ct = default)
    {
        var p = await _proposalRepo.FindWithVehiclesAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);

        if (p.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        if (p.Status != ProposalStatus.Active)
            throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_ACTIVE);

        p.BusinessType          = (BusinessType)req.BusinessType;
        p.PaymentType           = (PaymentType)req.PaymentType;
        p.Discount              = req.Discount;
        p.ProposalDate          = req.ProposalDate ?? p.ProposalDate;
        p.HasTradeIn            = req.HasTradeIn;
        p.TradeInType           = req.TradeInType is null ? null : (TradeInType)req.TradeInType;
        p.TradeInPlate          = req.TradeInPlate;
        p.TradeInBrand          = req.TradeInBrand;
        p.TradeInModel          = req.TradeInModel;
        p.TradeInYear           = req.TradeInYear;
        p.TradeInKm             = req.TradeInKm;
        p.TradeInEstimatedValue = req.TradeInEstimatedValue;

        foreach (var v in p.Vehicles.ToList())
            _proposalRepo.RemoveVehicle(v);

        var updatedVehicles = req.Vehicles?.ToList();
        AddVehicles(p, updatedVehicles);
        p.ProposalValue = ComputeProposalValue(updatedVehicles, req.ProposalValue);
        await _uow.SaveChangesAsync(ct);

        return await _proposalRepo.GetDtoByIdAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);
    }

    // ── Mark Lost ─────────────────────────────────────────────────────────
    public async Task<ProposalDto> MarkLostAsync(
        Guid id, Guid userId, MarkProposalLostRequest req, CancellationToken ct = default)
    {
        var p = await _proposalRepo.FindWithClientAndStageAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);

        if (p.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        if (p.Status != ProposalStatus.Active)
            throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_ACTIVE);

        p.Status     = ProposalStatus.Lost;
        p.LossReason = (LossReason)req.LossReason;
        p.LossNotes  = req.LossNotes;
        p.LostAt     = DateTimeOffset.UtcNow;

        var lostStage = await _stageRepo.FindLostStageAsync(userId, ct);
        if (lostStage is not null && !p.Client.CurrentStage.IsLost)
        {
            _clientRepo.AddHistory(new ClientStageHistory
            {
                ClientId    = p.ClientId,
                UserId      = userId,
                FromStageId = p.Client.CurrentStageId,
                ToStageId   = lostStage.Id,
                Obs         = p.LossNotes
            });

            p.Client.CurrentStageId    = lostStage.Id;
            p.Client.LastInteractionAt = DateTimeOffset.UtcNow;
        }

        await _uow.SaveChangesAsync(ct);

        return await _proposalRepo.GetDtoByIdAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);
    }

    // ── Convert to Sale ───────────────────────────────────────────────────
    // CRITICAL: SoldAt ALWAYS comes from req.SoldAt — NEVER auto-set to UtcNow
    public async Task<SaleDto> ConvertToSaleAsync(
        Guid id, Guid userId, ConvertToSaleRequest req, CancellationToken ct = default)
    {
        var p = await _proposalRepo.FindWithClientAndStageAsync(id, ct)
            ?? throw new ApiException(ApiErrorCatalog.PROPOSAL_NOT_FOUND);

        if (p.UserId != userId)
            throw new ApiException(ApiErrorCatalog.CLIENT_WRONG_USER);

        if (p.Status != ProposalStatus.Active)
        {
            var alreadyClosed = p.Status == ProposalStatus.Won || p.Status == ProposalStatus.Lost;
            throw new ApiException(alreadyClosed
                ? ApiErrorCatalog.PROPOSAL_ALREADY_CLOSED
                : ApiErrorCatalog.PROPOSAL_NOT_ACTIVE);
        }

        // Vehicle is required to convert to a sale
        if (req.ModelId == null && string.IsNullOrWhiteSpace(req.FreeTextModel))
            throw new ApiException(ApiErrorCatalog.SALE_MISSING_VEHICLE);

        // 1. Create Sale — SoldAt MUST come from req.SoldAt, never UtcNow
        var sale = new Sale
        {
            ProposalId    = p.Id,
            ClientId      = p.ClientId,
            UserId        = userId,
            ModelId       = req.ModelId,
            FreeTextModel = req.FreeTextModel,
            FinalValue    = req.FinalValue,
            PaymentType   = (PaymentType)req.PaymentType,
            SoldAt        = req.SoldAt,   // business date from user — NEVER UtcNow
            Plate         = req.Plate,
            Chassis       = req.Chassis,
            Obs           = req.Obs,
            Commission    = req.Commission
        };

        _saleRepo.Add(sale);

        // 2. Mark proposal as Won
        p.Status = ProposalStatus.Won;
        p.WonAt  = DateTimeOffset.UtcNow;

        // 3. Move client to Won stage, create history, schedule post-sale notification
        var wonStage = await _stageRepo.FindWonStageAsync(userId, ct);
        if (wonStage is not null)
        {
            _clientRepo.AddHistory(new ClientStageHistory
            {
                ClientId    = p.ClientId,
                UserId      = userId,
                FromStageId = p.Client.CurrentStageId,
                ToStageId   = wonStage.Id,
                Obs         = "Venda concluída"
            });

            p.Client.CurrentStageId    = wonStage.Id;
            p.Client.LastInteractionAt = DateTimeOffset.UtcNow;

            // 4. Schedule post-sale notification from preference
            var pref         = await _prefRepo.GetByUserAsync(userId, ct);
            var followUpDays = pref?.SaleFollowUpDays ?? 30;

            if (wonStage.Templates is not null)
            {
                foreach (var template in wonStage.Templates.Where(t => t.IsEnabled))
                {
                    _clientRepo.AddNotification(new Notification
                    {
                        UserId       = userId,
                        ClientId     = p.ClientId,
                        ProposalId   = p.Id,
                        SaleId       = sale.Id,
                        Trigger      = NotificationTrigger.SaleClosed,
                        Status       = NotificationStatus.Pending,
                        Title        = template.Title,
                        ScheduledFor = DateTimeOffset.UtcNow.AddDays(followUpDays)
                    });
                }
            }
        }

        await _uow.SaveChangesAsync(ct);

        // Cancel sibling proposals if requested
        if (req.CancelSiblingProposalIds?.Length > 0)
        {
            foreach (var siblingId in req.CancelSiblingProposalIds)
            {
                var sibling = await _proposalRepo.FindAsync(siblingId, ct);
                if (sibling is { Status: ProposalStatus.Active } && sibling.UserId == userId)
                {
                    sibling.Status = ProposalStatus.Cancelled;
                    sibling.LostAt = DateTimeOffset.UtcNow;
                }
            }
            await _uow.SaveChangesAsync(ct);
        }

        return await _saleRepo.GetDtoByIdAsync(sale.Id, userId, ct)
            ?? throw new ApiException(ApiErrorCatalog.SALE_NOT_FOUND);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static Proposal BuildProposal(Guid userId, Guid clientId, CreateProposalRequest req)
    {
        var vehicleList = req.Vehicles?.ToList();
        return new()
        {
            UserId                = userId,
            ClientId              = clientId,
            Status                = ProposalStatus.Active,
            BusinessType          = (BusinessType)req.BusinessType,
            PaymentType           = (PaymentType)req.PaymentType,
            ProposalValue         = ComputeProposalValue(vehicleList, req.ProposalValue),
            Discount              = req.Discount,
            ProposalDate          = req.ProposalDate ?? DateTimeOffset.UtcNow,
            HasTradeIn            = req.HasTradeIn,
            TradeInType           = req.TradeInType is null ? null : (TradeInType)req.TradeInType,
            TradeInPlate          = req.TradeInPlate,
            TradeInBrand          = req.TradeInBrand,
            TradeInModel          = req.TradeInModel,
            TradeInYear           = req.TradeInYear,
            TradeInKm             = req.TradeInKm,
            TradeInEstimatedValue = req.TradeInEstimatedValue
        };
    }

    /// <summary>
    /// When any vehicle has a Price, sum all vehicle prices as the proposal value.
    /// Falls back to the manually provided <paramref name="manualValue"/>.
    /// </summary>
    private static decimal? ComputeProposalValue(
        IList<ProposalVehicleRequest>? vehicles, decimal? manualValue)
    {
        if (vehicles is not null && vehicles.Any(v => v.Price.HasValue))
            return vehicles.Sum(v => v.Price ?? 0m);
        return manualValue;
    }

    private void AddVehicles(Proposal proposal, IEnumerable<ProposalVehicleRequest>? vehicles)
    {
        if (vehicles is null) return;
        _proposalRepo.AddVehicles(vehicles.Select(v => new ProposalVehicle
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
        }));
    }
}
