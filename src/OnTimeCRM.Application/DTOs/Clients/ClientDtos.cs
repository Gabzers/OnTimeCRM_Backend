using System.ComponentModel.DataAnnotations;

namespace OnTimeCRM.Application.DTOs.Clients;

// ── List DTO (lean) ───────────────────────────────────────────────────────────
public record ClientListDto(
    Guid Id,
    string FullName,
    string? Phone,
    string? Email,
    int LeadSource,
    int Temperature,
    Guid CurrentStageId,
    string CurrentStageName,
    string? CurrentStageColor,
    DateTimeOffset? LastInteractionAt,
    DateTimeOffset CreatedAt
);

// ── Detail DTO ────────────────────────────────────────────────────────────────
public record ClientDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? TaxId,
    int LeadSource,
    Guid CurrentStageId,
    string CurrentStageName,
    string? CurrentStageColor,
    bool CurrentStageIsFinal,
    bool CurrentStageIsWon,
    bool CurrentStageIsLost,
    int Temperature,
    DateTimeOffset? LastInteractionAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

// ── Requests ──────────────────────────────────────────────────────────────────
public record CreateClientRequest(
    [Required] string FullName,
    string? Email = null,
    string? Phone = null,
    string? TaxId = null,
    /// <summary>Optional. Defaults to 0 (Stand/Walk-In) if not provided.</summary>
    int? LeadSource = null,
    /// <summary>Optional. If provided uses this stage; otherwise defaults to Order=2 (Agendar Test Drive).</summary>
    Guid? StageId = null,
    string? Notes = null,
    CreateProposalReq? Proposal = null
);

/// <summary>Nested proposal data sent at client creation.</summary>
public record CreateProposalReq(
    int BusinessType = 0,
    int PaymentType = 0,
    decimal? ProposalValue = null,
    decimal? Discount = null,
    DateTimeOffset? ProposalDate = null,
    bool HasTradeIn = false,
    TradeInReq? TradeIn = null,
    IEnumerable<ProposalVehicleRequest>? Vehicles = null
);

public record TradeInReq(
    string? Plate = null,
    string? Brand = null,
    string? Model = null,
    int? Year = null,
    int? Km = null,
    decimal? EstimatedValue = null,
    int? Type = null
);

public record ProposalVehicleRequest(
    Guid? ModelId = null,
    string? FreeTextModel = null,
    bool IsPreferred = false,
    /// <summary>Price for this specific vehicle in the proposal.</summary>
    decimal? Price = null,
    /// <summary>Discount applied to this specific vehicle.</summary>
    decimal? Discount = null,
    Guid? VersionId = null,
    string? ExternalColor = null,
    string? InternalColor = null
);

public record UpdateClientStageRequest(
    [Required] Guid StageId,
    string? Obs
);

// ── Stage History ─────────────────────────────────────────────────────────────
public record StageHistoryDto(
    Guid Id,
    Guid? FromStageId,
    string? FromStageName,
    Guid ToStageId,
    string ToStageName,
    string? ToStageColor,
    string? Obs,
    string? ProposalSnapshot,
    DateTimeOffset CreatedAt
);

// ── Filter Params ─────────────────────────────────────────────────────────────
public record ClientFilterParams(
    Guid? StageId = null,
    int? Temperature = null,
    int? LeadSource = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);
