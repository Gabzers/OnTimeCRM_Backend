using System.ComponentModel.DataAnnotations;
using OnTimeCRM.Application.DTOs.Clients;

namespace OnTimeCRM.Application.DTOs.Proposals;

public record ProposalVehicleDto(
    Guid Id,
    Guid? ModelId,
    string? ModelName,
    string? ModelBrandName,
    string? FreeTextModel,
    bool IsPreferred,
    decimal? Price,
    decimal? Discount,
    Guid? VersionId,
    string? VersionName,
    string? ExternalColor,
    string? InternalColor
);

public record ProposalDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    int Status,
    int BusinessType,
    int PaymentType,
    decimal? ProposalValue,
    decimal? Discount,
    DateTimeOffset? ProposalDate,
    int? LossReason,
    string? LossNotes,
    DateTimeOffset? WonAt,
    DateTimeOffset? LostAt,
    bool HasTradeIn,
    int? TradeInType,
    string? TradeInPlate,
    string? TradeInBrand,
    string? TradeInModel,
    int? TradeInYear,
    int? TradeInKm,
    decimal? TradeInEstimatedValue,
    IEnumerable<ProposalVehicleDto> Vehicles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ProposalListDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    int Status,
    int BusinessType,
    int PaymentType,
    decimal? ProposalValue,
    DateTimeOffset? ProposalDate,
    DateTimeOffset CreatedAt,
    string? VehicleName = null
);

public record CreateProposalRequest(
    int BusinessType,
    int PaymentType,
    /// <summary>Computed automatically as sum of vehicle prices when Vehicles provided. Manual otherwise.</summary>
    decimal? ProposalValue,
    decimal? Discount,
    DateTimeOffset? ProposalDate,
    bool HasTradeIn,
    int? TradeInType,
    string? TradeInPlate,
    string? TradeInBrand,
    string? TradeInModel,
    int? TradeInYear,
    int? TradeInKm,
    decimal? TradeInEstimatedValue,
    IEnumerable<ProposalVehicleRequest>? Vehicles
);

public record MarkProposalLostRequest(
    [Required] int LossReason,
    string? LossNotes
);

public record ConvertToSaleRequest(
    /// <summary>
    /// User-provided deal closing date. NEVER defaults to UtcNow — always comes from this field.
    /// </summary>
    [Required] DateTimeOffset SoldAt,
    [Required] decimal FinalValue,
    int PaymentType = 0,
    Guid? ModelId = null,
    string? FreeTextModel = null,
    string? Plate = null,
    string? Chassis = null,
    string? Obs = null,
    /// <summary>Salesperson's commission for this sale. Private — never exposed to friends.</summary>
    decimal? Commission = null,
    /// <summary>IDs of other active proposals to cancel when this one is converted to a sale.</summary>
    Guid[]? CancelSiblingProposalIds = null
);

public record ProposalFilterParams(
    int? Status = null,
    int? BusinessType = null,
    int? PaymentType = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? Search = null,
    Guid? ClientId = null,
    Guid? StageId = null,
    int Page = 1,
    int PageSize = 20
);
