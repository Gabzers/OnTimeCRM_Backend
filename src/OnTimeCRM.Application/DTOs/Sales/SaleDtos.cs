namespace OnTimeCRM.Application.DTOs.Sales;

public record SaleDto(
    Guid Id,
    Guid ProposalId,
    Guid ClientId,
    string ClientName,
    string? ClientPhone,
    Guid? ModelId,
    string? ModelName,
    string? FreeTextModel,
    decimal FinalValue,
    int PaymentType,
    DateTimeOffset SoldAt,
    string? Plate,
    string? Chassis,
    string? Obs,
    /// <summary>Commission is private — only returned to the owner; never included in friend public profiles.</summary>
    decimal? Commission,
    DateTimeOffset CreatedAt
);

public record SaleListDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string? ModelName,
    string? FreeTextModel,
    decimal FinalValue,
    int PaymentType,
    DateTimeOffset SoldAt,
    string? Plate = null,
    decimal? Commission = null
);

public record ClientSaleHistoryDto(
    Guid Id,
    string? ModelName,
    string? FreeTextModel,
    decimal FinalValue,
    int PaymentType,
    DateTimeOffset SoldAt
);

public record SaleFilterParams(
    int? Year = null,
    int? Month = null,
    int Page = 1,
    int PageSize = 20
);

public record DashboardDto(
    int ActiveClients,
    int ProposalsThisMonth,
    int SalesThisMonth,
    decimal RevenueThisMonth,
    decimal ConversionRate,
    decimal CommissionThisMonth,
    IEnumerable<MonthlyStatDto> MonthlySales,
    IEnumerable<LossReasonStatDto> LossReasons,
    IEnumerable<object> HotDeals,
    int OverdueNotificationsCount
);

public record MonthlyStatDto(int Year, int Month, int Proposals, int Sales, decimal Revenue);
public record LossReasonStatDto(int LossReason, int Count);
