using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Goals;
using OnTime.Application.Interfaces;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;

namespace OnTime.Application.Services;

public class UserGoalService : IUserGoalService
{
    private readonly IAppDbContext  _db;
    private readonly IUnitOfWork    _uow;

    public UserGoalService(IAppDbContext db, IUnitOfWork uow)
    {
        _db  = db;
        _uow = uow;
    }

    public async Task<IEnumerable<GoalProgressDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default)
    {
        var goals = await _db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        if (!goals.Any()) return [];

        var results = new List<GoalProgressDto>();
        foreach (var g in goals)
        {
            var current = await ComputeCurrentValueAsync(g, userId, ct);
            var pct = g.TargetValue > 0
                ? Math.Min(Math.Round(current / g.TargetValue * 100m, 1), 100m)
                : 0m;
            results.Add(new GoalProgressDto(ToDto(g), current, pct));
        }
        return results;
    }

    /// <summary>
    /// Computes progress from the goal's own date range — never a fixed "this month" snapshot,
    /// so Daily/Weekly/Annual goals (and goals with a custom EndDate) get a value that actually
    /// matches their period instead of always reading whatever the calendar month looks like.
    /// </summary>
    private async Task<decimal> ComputeCurrentValueAsync(UserGoal g, Guid userId, CancellationToken ct)
    {
        var start = g.StartDate;
        var end   = g.EndDate ?? PeriodEnd(g.Period, g.StartDate);

        switch (g.MetricType)
        {
            case GoalMetricType.Sales:
                return await _db.Sales.CountAsync(s =>
                    s.UserId == userId && s.SoldAt >= start && s.SoldAt < end, ct);

            case GoalMetricType.Proposals:
                return await _db.Proposals.CountAsync(p =>
                    p.UserId == userId && p.ProposalDate >= start && p.ProposalDate < end, ct);

            case GoalMetricType.NewClients:
                return await _db.Clients.CountAsync(c =>
                    c.UserId == userId && c.CreatedAt >= start && c.CreatedAt < end, ct);

            case GoalMetricType.ConversionRate:
                var proposalsInRange = await _db.Proposals.CountAsync(p =>
                    p.UserId == userId && p.ProposalDate >= start && p.ProposalDate < end, ct);
                if (proposalsInRange == 0) return 0m;
                var salesInRange = await _db.Sales.CountAsync(s =>
                    s.UserId == userId && s.SoldAt >= start && s.SoldAt < end, ct);
                return Math.Round((decimal)salesInRange / proposalsInRange * 100m, 1);

            default:
                return 0m;
        }
    }

    private static DateTimeOffset PeriodEnd(GoalPeriod period, DateTimeOffset start) => period switch
    {
        GoalPeriod.Daily   => start.AddDays(1),
        GoalPeriod.Weekly  => start.AddDays(7),
        GoalPeriod.Monthly => start.AddMonths(1),
        GoalPeriod.Annual  => start.AddYears(1),
        _                  => start.AddMonths(1)
    };

    public async Task<UserGoalDto> CreateGoalAsync(Guid userId, CreateUserGoalRequest request, CancellationToken ct = default)
    {
        if (request.MetricType == (int)GoalMetricType.ConversionRate && request.TargetValue > 100)
            throw new ApiException(ApiErrorCatalog.GOAL_PERCENT_OUT_OF_RANGE);

        var maxOrder = await _db.UserGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .Select(g => (int?)g.SortOrder)
            .MaxAsync(ct);

        var goal = new UserGoal
        {
            UserId      = userId,
            MetricType  = (GoalMetricType)request.MetricType,
            Period      = (GoalPeriod)request.Period,
            TargetValue = request.TargetValue,
            StartDate   = request.StartDate,
            EndDate     = request.EndDate,
            ShowOnDashboard = request.ShowOnDashboard,
            SortOrder   = (maxOrder ?? -1) + 1,
        };

        _db.UserGoals.Add(goal);
        await _uow.SaveChangesAsync(ct);
        return ToDto(goal);
    }

    public async Task ReorderGoalsAsync(Guid userId, ReorderGoalsRequest request, CancellationToken ct = default)
    {
        var goals = await _db.UserGoals
            .Where(g => g.UserId == userId && g.IsActive)
            .ToListAsync(ct);

        var goalsById = goals.ToDictionary(g => g.Id);
        for (var i = 0; i < request.OrderedIds.Count; i++)
        {
            if (goalsById.TryGetValue(request.OrderedIds[i], out var goal))
                goal.SortOrder = i;
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<UserGoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateUserGoalRequest request, CancellationToken ct = default)
    {
        var goal = await FindOwnedAsync(userId, goalId, ct);

        if (goal.MetricType == GoalMetricType.ConversionRate && request.TargetValue > 100)
            throw new ApiException(ApiErrorCatalog.GOAL_PERCENT_OUT_OF_RANGE);

        goal.TargetValue     = request.TargetValue;
        goal.StartDate       = request.StartDate;
        goal.EndDate         = request.EndDate;
        goal.ShowOnDashboard = request.ShowOnDashboard;
        await _uow.SaveChangesAsync(ct);
        return ToDto(goal);
    }

    public async Task DeleteGoalAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await FindOwnedAsync(userId, goalId, ct);
        goal.IsActive = false;
        await _uow.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<UserGoal> FindOwnedAsync(Guid userId, Guid goalId, CancellationToken ct)
    {
        var goal = await _db.UserGoals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null || !goal.IsActive)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);
        if (goal.UserId != userId)
            throw new ApiException(ApiErrorCatalog.AUTH_FORBIDDEN);
        return goal;
    }

    private static UserGoalDto ToDto(UserGoal g) =>
        new(g.Id, (int)g.MetricType, (int)g.Period, g.TargetValue, g.StartDate, g.EndDate, g.ShowOnDashboard, g.CreatedAt, g.SortOrder);
}
