using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Clients;
using OnTime.Application.DTOs.Sales;
using OnTime.Application.Interfaces.Repositories;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Infrastructure.Persistence;

namespace OnTime.Infrastructure.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _db;

    public SaleRepository(AppDbContext db) => _db = db;

    // ── Paginated list ────────────────────────────────────────────────────
    public async Task<PagedResult<SaleListDto>> GetPagedAsync(
        Guid userId,
        SaleFilterParams filter,
        CancellationToken ct = default)
    {
        var query = _db.Sales
            .AsNoTracking()
            .Include(s => s.Client)
            .Include(s => s.Model)
            .Where(s => s.UserId == userId);

        // Range comparison instead of EXTRACT(year/month FROM sold_at) so a plain
        // btree index on SoldAt can actually be used by the planner.
        if (filter.Year.HasValue)
        {
            var month = filter.Month ?? 1;
            var rangeStart = new DateTimeOffset(filter.Year.Value, month, 1, 0, 0, 0, TimeSpan.Zero);
            var rangeEnd = filter.Month.HasValue
                ? rangeStart.AddMonths(1)
                : rangeStart.AddYears(1);
            query = query.Where(s => s.SoldAt >= rangeStart && s.SoldAt < rangeEnd);
        }
        else if (filter.Month.HasValue)
        {
            query = query.Where(s => s.SoldAt.Month == filter.Month.Value);
        }

        if (filter.Delivered.HasValue)
        {
            query = filter.Delivered.Value
                ? query.Where(s => s.DeliveredAt != null)
                : query.Where(s => s.DeliveredAt == null);
        }

        var total = await query.CountAsync(ct);
        var size  = Math.Clamp(filter.PageSize, 1, 50);

        var items = await query
            .OrderByDescending(s => s.SoldAt)
            .Skip((filter.Page - 1) * size)
            .Take(size)
            .Select(s => new SaleListDto(
                s.Id,
                s.ClientId,
                s.Client.FullName,
                s.Model != null ? $"{s.Model.VehicleBrand.Name} {s.Model.Name}" : null,
                s.FreeTextModel,
                s.FinalValue,
                (int)s.PaymentType,
                s.SoldAt,
                s.Plate,
                s.Commission,
                s.EstimatedDeliveryDate,
                s.DeliveredAt))
            .ToListAsync(ct);

        return new PagedResult<SaleListDto>(items, total, filter.Page, size);
    }

    // ── Single sale DTO ───────────────────────────────────────────────────
    public async Task<SaleDto?> GetDtoByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var s = await _db.Sales
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Model)
                .ThenInclude(m => m!.VehicleBrand)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

        if (s is null) return null;

        return new SaleDto(
            s.Id, s.ProposalId, s.ClientId, s.Client.FullName, s.Client.Phone,
            s.ModelId,
            s.Model != null ? $"{s.Model.VehicleBrand.Name} {s.Model.Name}" : null,
            s.FreeTextModel,
            s.FinalValue, (int)s.PaymentType,
            s.SoldAt,
            s.Plate, s.Chassis, s.Obs,
            s.Commission,
            s.CreatedAt,
            s.EstimatedDeliveryDate,
            s.DeliveredAt);
    }

    public async Task<Sale?> FindAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Sales
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    // ── Dashboard ────────────────────────────────────────────────────────
    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        // 1. KPIs
        var activeClients = await _db.Clients
            .Where(c => c.UserId == userId && c.IsActive && !c.CurrentStage.IsFinal)
            .CountAsync(ct);

        var proposalsThisMonth = await _db.Proposals
            .Where(p => p.UserId == userId && p.ProposalDate >= monthStart && p.ProposalDate < monthEnd)
            .CountAsync(ct);

        var salesThisMonthQuery = _db.Sales
            .Where(s => s.UserId == userId && s.SoldAt >= monthStart && s.SoldAt < monthEnd);
        var salesThisMonth = await salesThisMonthQuery.CountAsync(ct);
        var revenueThisMonth = await salesThisMonthQuery.SumAsync(s => (decimal?)s.FinalValue, ct) ?? 0m;
        var commissionThisMonth = await salesThisMonthQuery.SumAsync(s => (decimal?)s.Commission, ct) ?? 0m;

        var overdueCount = await _db.Notifications
            .Where(n => n.UserId == userId && n.Status == NotificationStatus.Pending && n.ScheduledFor < now)
            .CountAsync(ct);

        var conversionRate = proposalsThisMonth > 0
            ? Math.Round((decimal)salesThisMonth / proposalsThisMonth * 100m, 1)
            : 0m;

        // 2. Monthly stats — last 6 months including the current one, zero-filled for months
        // with no activity (mirrors the old fn_get_monthly_stats' month_series + LEFT JOIN).
        const int months = 6;
        var rangeStart = monthStart.AddMonths(-(months - 1));

        var salesInRange = await _db.Sales
            .Where(s => s.UserId == userId && s.SoldAt >= rangeStart)
            .Select(s => new { s.SoldAt, s.FinalValue })
            .ToListAsync(ct);

        var proposalsInRange = await _db.Proposals
            .Where(p => p.UserId == userId && p.ProposalDate >= rangeStart)
            .Select(p => p.ProposalDate)
            .ToListAsync(ct);

        var monthlyStat = new List<MonthlyStatDto>();
        for (var i = 0; i < months; i++)
        {
            var monthDate = rangeStart.AddMonths(i);
            var salesForMonth = salesInRange.Where(s => s.SoldAt.Year == monthDate.Year && s.SoldAt.Month == monthDate.Month).ToList();
            var proposalsForMonth = proposalsInRange.Count(p => p.HasValue && p.Value.Year == monthDate.Year && p.Value.Month == monthDate.Month);
            monthlyStat.Add(new MonthlyStatDto(
                monthDate.Year, monthDate.Month,
                proposalsForMonth, salesForMonth.Count,
                salesForMonth.Sum(s => s.FinalValue)));
        }

        // 3. Loss reasons
        // GroupBy + projecting into a DTO, then ordering by the DTO's property, doesn't
        // translate to SQL — materialize the grouped counts first, sort client-side.
        var lossReasonCounts = await _db.Proposals
            .Where(p => p.UserId == userId && p.Status == ProposalStatus.Lost && p.LossReason != null)
            .GroupBy(p => p.LossReason!.Value)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var lossReasons = lossReasonCounts
            .OrderByDescending(r => r.Count)
            .Select(r => new LossReasonStatDto((int)r.Reason, r.Count))
            .ToList();

        // 4. Hot deals (top 5) — same Hot/non-final-stage rule as ClientRepository.GetHotDealsAsync.
        const int hot = 0; // DealTemperature.Hot
        var hotDeals = await _db.Clients
            .Where(c => c.UserId == userId && c.IsActive && (int)c.Temperature == hot && !c.CurrentStage.IsFinal)
            .OrderByDescending(c => c.LastInteractionAt)
            .Take(5)
            .Select(c => new
            {
                id                = c.Id,
                fullName          = c.FullName,
                phone             = c.Phone,
                stageName         = c.CurrentStage.Name,
                stageColor        = c.CurrentStage.Color,
                lastInteractionAt = c.LastInteractionAt,
            })
            .ToListAsync(ct);

        return new DashboardDto(
            activeClients,
            proposalsThisMonth,
            salesThisMonth,
            revenueThisMonth,
            conversionRate,
            commissionThisMonth,
            monthlyStat,
            lossReasons,
            hotDeals,
            overdueCount);
    }

    // ── Writes ────────────────────────────────────────────────────────────
    public void Add(Sale sale) => _db.Sales.Add(sale);
}
