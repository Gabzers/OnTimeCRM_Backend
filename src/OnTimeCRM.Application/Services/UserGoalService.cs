using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Goals;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Domain.Entities;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.Application.Services;

public class UserGoalService : IUserGoalService
{
    private readonly IAppDbContext  _db;
    private readonly ISaleService   _sales;
    private readonly IUnitOfWork    _uow;

    public UserGoalService(IAppDbContext db, ISaleService sales, IUnitOfWork uow)
    {
        _db    = db;
        _sales = sales;
        _uow   = uow;
    }

    public async Task<IEnumerable<GoalProgressDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default)
    {
        var goals = await _db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        if (!goals.Any()) return [];

        // Fetch current KPIs to compute progress
        var dashboard = await _sales.GetDashboardAsync(userId, ct);

        return goals.Select(g =>
        {
            var current = (GoalMetricType)g.MetricType switch
            {
                GoalMetricType.Sales          => (decimal)dashboard.SalesThisMonth,
                GoalMetricType.Proposals      => (decimal)dashboard.ProposalsThisMonth,
                GoalMetricType.NewClients     => (decimal)dashboard.ActiveClients,
                GoalMetricType.ConversionRate => dashboard.ConversionRate,
                _                             => 0m
            };

            var pct = g.TargetValue > 0
                ? Math.Min(Math.Round(current / g.TargetValue * 100m, 1), 100m)
                : 0m;

            return new GoalProgressDto(ToDto(g), current, pct);
        });
    }

    public async Task<UserGoalDto> CreateGoalAsync(Guid userId, CreateUserGoalRequest request, CancellationToken ct = default)
    {
        var goal = new UserGoal
        {
            UserId      = userId,
            MetricType  = (GoalMetricType)request.MetricType,
            Period      = (GoalPeriod)request.Period,
            TargetValue = request.TargetValue,
            StartDate   = request.StartDate,
            EndDate     = request.EndDate,
        };

        _db.UserGoals.Add(goal);
        await _uow.SaveChangesAsync(ct);
        return ToDto(goal);
    }

    public async Task<UserGoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateUserGoalRequest request, CancellationToken ct = default)
    {
        var goal = await FindOwnedAsync(userId, goalId, ct);
        goal.TargetValue = request.TargetValue;
        goal.StartDate   = request.StartDate;
        goal.EndDate     = request.EndDate;
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
        new(g.Id, (int)g.MetricType, (int)g.Period, g.TargetValue, g.StartDate, g.EndDate, g.CreatedAt);
}
