using Microsoft.EntityFrameworkCore;
using OnTime.Application.Interfaces;
using OnTime.Domain.Enums;

namespace OnTime.Application.Common;

/// <summary>
/// Given an arbitrary [start, end) window, computes a goal's metric value in that window.
/// Extracted out of UserGoalService so the business-summary email can reuse the exact same
/// per-metric-type logic for both a goal's *live* window and a *past, already-closed* window
/// (to report how a just-finished cycle went) — one implementation, no drift between the two.
/// </summary>
public static class GoalProgressCalculator
{
    public static async Task<decimal> ComputeValueAsync(
        IAppDbContext db, Guid userId, GoalMetricType metricType,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        switch (metricType)
        {
            case GoalMetricType.Sales:
                return await db.Sales.CountAsync(s =>
                    s.UserId == userId && s.SoldAt >= start && s.SoldAt < end, ct);

            case GoalMetricType.Proposals:
                return await db.Proposals.CountAsync(p =>
                    p.UserId == userId && p.ProposalDate >= start && p.ProposalDate < end, ct);

            case GoalMetricType.NewClients:
                return await db.Clients.CountAsync(c =>
                    c.UserId == userId && c.CreatedAt >= start && c.CreatedAt < end, ct);

            case GoalMetricType.ConversionRate:
                var proposalsInRange = await db.Proposals.CountAsync(p =>
                    p.UserId == userId && p.ProposalDate >= start && p.ProposalDate < end, ct);
                if (proposalsInRange == 0) return 0m;
                var salesInRange = await db.Sales.CountAsync(s =>
                    s.UserId == userId && s.SoldAt >= start && s.SoldAt < end, ct);
                return Math.Round((decimal)salesInRange / proposalsInRange * 100m, 1);

            default:
                return 0m;
        }
    }
}
