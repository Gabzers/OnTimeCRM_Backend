using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.Interfaces;
using OnTime.Domain.Entities;
using OnTime.Domain.Enums;
using OnTime.Domain.Services;

namespace OnTime.Application.Services;

/// <summary>
/// Runs the two unattended passes described in
/// 04-DECISIONS/2026-06-30-stage-driven-temperature-and-notifications.md, triggered by the
/// Supabase pg_cron job hitting POST /api/internal/run-scheduled-jobs. Both passes are scoped to
/// only the rows that can possibly be due — no full table scans.
/// </summary>
public class ScheduledJobsService : IScheduledJobsService
{
    private readonly IAppDbContext _db;
    private readonly IEmailSender _emailSender;

    public ScheduledJobsService(IAppDbContext db, IEmailSender emailSender)
    {
        _db = db;
        _emailSender = emailSender;
    }

    public async Task<ScheduledJobsRunResult> RunAsync(CancellationToken ct = default)
    {
        var temperatureTransitions = await RunTemperatureTransitionsAsync(ct);
        var (notificationsGenerated, seriesCompleted) = await RunRecurringNotificationsAsync(ct);
        var digestEmailsSent = await RunDigestEmailsAsync(ct);
        var businessSummariesSent = await RunBusinessSummaryEmailsAsync(ct);

        await _db.SaveChangesAsync(ct);

        return new ScheduledJobsRunResult(temperatureTransitions, notificationsGenerated, seriesCompleted, digestEmailsSent, businessSummariesSent);
    }

    // ── Pass 1: stage-driven temperature transitions ────────────────────────
    private async Task<int> RunTemperatureTransitionsAsync(CancellationToken ct)
    {
        var stagesWithRules = await _db.ClientStages
            .Where(s => s.AffectsTemperature)
            .Include(s => s.TemperatureRules)
            .ToListAsync(ct);

        if (stagesWithRules.Count == 0) return 0;

        var stageIds = stagesWithRules.Select(s => s.Id).ToList();
        var clients = await _db.Clients
            .Where(c => c.IsActive && stageIds.Contains(c.CurrentStageId))
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var transitions = 0;

        foreach (var client in clients)
        {
            var stage = stagesWithRules.First(s => s.Id == client.CurrentStageId);
            var enteredAt = client.LastInteractionAt ?? client.CreatedAt;
            var daysSinceEntry = (now - enteredAt).TotalDays;

            var effective = StageTemperatureCalculator.EffectiveTemperature(
                daysSinceEntry,
                stage.TemperatureRules.Select(r => (r.DaysAfterEntry, r.Temperature)));

            if (effective.HasValue && (int)client.Temperature != effective.Value)
            {
                client.Temperature = (DealTemperature)effective.Value;
                transitions++;
            }
        }

        return transitions;
    }

    // ── Pass 2: recurring notifications ──────────────────────────────────────
    private async Task<(int generated, int completed)> RunRecurringNotificationsAsync(CancellationToken ct)
    {
        var activeSeries = await _db.ClientStageNotificationSeries
            .Where(s => s.IsActive)
            .Include(s => s.Template)
            .Include(s => s.ClientStageHistory)
            .ToListAsync(ct);

        if (activeSeries.Count == 0) return (0, 0);

        var now = DateTimeOffset.UtcNow;
        var generated = 0;
        var completed = 0;

        foreach (var series in activeSeries)
        {
            var template = series.Template;
            var lastFired = series.LastFiredAt ?? series.ClientStageHistory.CreatedAt;
            var nextDue = NextOccurrence(lastFired, template);

            if (nextDue is null || now < nextDue.Value) continue;

            _db.Notifications.Add(new Notification
            {
                UserId       = series.ClientStageHistory.UserId,
                ClientId     = series.ClientStageHistory.ClientId,
                Trigger      = NotificationTrigger.StageChanged,
                Status       = NotificationStatus.Pending,
                Title        = template.Title,
                ScheduledFor = now
            });
            generated++;

            series.OccurrenceCount++;
            series.LastFiredAt = now;

            if (template.MaxOccurrences.HasValue && series.OccurrenceCount >= template.MaxOccurrences.Value)
            {
                series.IsActive = false;
                completed++;
            }
        }

        return (generated, completed);
    }

    // ── Pass 3: daily/periodic digest emails ─────────────────────────────────
    private async Task<int> RunDigestEmailsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var candidates = await _db.NotificationPreferences
            .Where(p => p.DigestEnabled && p.EmailOnGeneralNotifications)
            .Include(p => p.User)
            .ToListAsync(ct);

        var sent = 0;

        foreach (var pref in candidates)
        {
            if (!IsDigestDue(pref, now)) continue;

            var pending = await _db.Notifications
                .Where(n => n.UserId == pref.UserId
                    && n.Status == Domain.Enums.NotificationStatus.Pending
                    && n.ScheduledFor <= now)
                .OrderBy(n => n.ScheduledFor)
                .Take(10)
                .ToListAsync(ct);

            // Still mark as checked even with nothing pending — avoids re-evaluating (and
            // potentially re-sending) on every subsequent cron tick until the next cadence window.
            if (pending.Count == 0)
            {
                pref.LastDigestSentAt = now;
                continue;
            }

            var subject = EmailTemplates.DigestSubject(pref.User.Locale);
            var html = EmailTemplates.DigestBody(pref.User.Locale, pref.User.FullName, pending.Select(n => n.Title).ToList());
            await _emailSender.SendAsync(pref.User.Email, pref.User.FullName, subject, html, "Digest", ct);
            pref.LastDigestSentAt = now;
            sent++;
        }

        return sent;
    }

    private static bool IsDigestDue(NotificationPreference pref, DateTimeOffset now)
    {
        if (now.TimeOfDay < pref.DailyDigestTime.ToTimeSpan()) return false;

        if (pref.DigestDaysOfWeek != 0)
        {
            var todayBit = 1 << (int)now.DayOfWeek;
            if ((pref.DigestDaysOfWeek & todayBit) == 0) return false;
            return pref.LastDigestSentAt is null || pref.LastDigestSentAt.Value.Date < now.Date;
        }

        return pref.LastDigestSentAt is null
            || (now.Date - pref.LastDigestSentAt.Value.Date).Days >= pref.DigestFrequencyDays;
    }

    // ── Pass 4: business summary emails (weekly/monthly) ─────────────────────
    private async Task<int> RunBusinessSummaryEmailsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var candidates = await _db.NotificationPreferences
            .Where(p => p.BusinessSummaryEnabled)
            .Include(p => p.User)
            .ToListAsync(ct);

        var sent = 0;

        foreach (var pref in candidates)
        {
            if (!IsBusinessSummaryDue(pref, now)) continue;

            var (periodStart, periodEnd) = BusinessSummaryWindow(pref, now);
            var userId = pref.UserId;

            BusinessSummaryCounts? counts = null;
            if (pref.BusinessSummaryIncludeCounts)
            {
                var newClients = await _db.Clients.CountAsync(c =>
                    c.UserId == userId && c.CreatedAt >= periodStart && c.CreatedAt < periodEnd, ct);
                var salesInRange = await _db.Sales
                    .Where(s => s.UserId == userId && s.SoldAt >= periodStart && s.SoldAt < periodEnd)
                    .ToListAsync(ct);
                counts = new BusinessSummaryCounts(newClients, salesInRange.Count, salesInRange.Sum(s => s.Commission ?? 0m));
            }

            List<BusinessSummaryStageCount>? stageBreakdown = null;
            if (pref.BusinessSummaryIncludeStageSummary)
            {
                // GroupBy + projecting straight into the record doesn't translate to SQL —
                // materialize the grouped counts as an anonymous type first (same pattern as
                // SaleRepository's loss-reasons query), then map to the record client-side.
                var grouped = await _db.Clients
                    .Where(c => c.UserId == userId && c.IsActive && !c.CurrentStage.IsFinal)
                    .GroupBy(c => c.CurrentStage.Name)
                    .Select(g => new { StageName = g.Key, Count = g.Count() })
                    .ToListAsync(ct);
                stageBreakdown = grouped
                    .OrderByDescending(s => s.Count)
                    .Select(s => new BusinessSummaryStageCount(s.StageName, s.Count))
                    .ToList();
            }

            List<BusinessSummaryGoalLine>? goalLines = null;
            if (pref.BusinessSummaryIncludeGoals)
            {
                var activeGoals = await _db.UserGoals
                    .Where(g => g.UserId == userId && g.IsActive)
                    .ToListAsync(ct);

                goalLines = new List<BusinessSummaryGoalLine>();
                foreach (var g in activeGoals)
                {
                    var (liveStart, liveEnd) = GoalPeriodCalculator.Window(g.Period, now);
                    var liveValue = await GoalProgressCalculator.ComputeValueAsync(_db, userId, g.MetricType, liveStart, liveEnd, ct);
                    var livePct = g.TargetValue > 0 ? Math.Min(Math.Round(liveValue / g.TargetValue * 100m, 1), 100m) : 0m;

                    // Did this goal's own cycle close inside the reported window? (e.g. a Weekly
                    // goal reported inside a Monthly summary will have closed 3-4 times.) Report
                    // the most recently closed one only — a full history isn't the point here.
                    decimal? completedValue = null, completedTarget = null, completedPct = null;
                    var (prevStart, prevEnd) = GoalPeriodCalculator.Window(g.Period, periodEnd.AddTicks(-1));
                    if (prevEnd > periodStart && prevEnd <= periodEnd)
                    {
                        completedValue = await GoalProgressCalculator.ComputeValueAsync(_db, userId, g.MetricType, prevStart, prevEnd, ct);
                        completedTarget = g.TargetValue;
                        completedPct = g.TargetValue > 0 ? Math.Min(Math.Round(completedValue.Value / g.TargetValue * 100m, 1), 100m) : 0m;
                    }

                    goalLines.Add(new BusinessSummaryGoalLine(
                        g.MetricType, g.Period, liveValue, g.TargetValue, livePct,
                        completedValue, completedTarget, completedPct));
                }
            }

            var subject = EmailTemplates.BusinessSummarySubject(pref.User.Locale, pref.BusinessSummaryFrequency);
            var html = EmailTemplates.BusinessSummaryBody(
                pref.User.Locale, pref.User.FullName, periodStart, periodEnd, counts, stageBreakdown, goalLines);
            await _emailSender.SendAsync(pref.User.Email, pref.User.FullName, subject, html, "BusinessSummary", ct);
            pref.LastBusinessSummarySentAt = now;
            sent++;
        }

        return sent;
    }

    private static bool IsBusinessSummaryDue(NotificationPreference pref, DateTimeOffset now)
    {
        if ((int)now.DayOfWeek != pref.BusinessSummaryDayOfWeek) return false;

        if (pref.BusinessSummaryFrequency == SummaryFrequency.Weekly)
        {
            var weekStart = GoalPeriodCalculator.StartOfWeek(new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero));
            return pref.LastBusinessSummarySentAt is null || pref.LastBusinessSummarySentAt.Value < weekStart;
        }

        // Monthly: only the first occurrence of the chosen weekday in the month.
        if (now.Day > 7) return false;
        return pref.LastBusinessSummarySentAt is null
            || pref.LastBusinessSummarySentAt.Value.Month != now.Month
            || pref.LastBusinessSummarySentAt.Value.Year != now.Year;
    }

    private static (DateTimeOffset start, DateTimeOffset end) BusinessSummaryWindow(NotificationPreference pref, DateTimeOffset now)
    {
        if (pref.BusinessSummaryFrequency == SummaryFrequency.Weekly)
        {
            var currentWeekStart = GoalPeriodCalculator.StartOfWeek(new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero));
            return (currentWeekStart.AddDays(-7), currentWeekStart);
        }

        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (currentMonthStart.AddMonths(-1), currentMonthStart);
    }

    private static DateTimeOffset? NextOccurrence(DateTimeOffset lastFired, StageNotificationTemplate t)
    {
        if (t.RecurrenceIntervalDays.HasValue)
            return lastFired.AddDays(t.RecurrenceIntervalDays.Value);

        if (t.FixedDayOfWeek.HasValue)
        {
            var daysAhead = ((int)t.FixedDayOfWeek.Value - (int)lastFired.DayOfWeek + 7) % 7;
            daysAhead = daysAhead == 0 ? 7 : daysAhead;  // always strictly in the future from lastFired
            return lastFired.AddDays(daysAhead);
        }

        if (t.FixedDayOfMonth.HasValue)
        {
            var candidate = new DateTimeOffset(lastFired.Year, lastFired.Month, 1, 0, 0, 0, lastFired.Offset)
                .AddMonths(1)
                .AddDays(Math.Min(t.FixedDayOfMonth.Value, DateTime.DaysInMonth(lastFired.Year, lastFired.Month)) - 1);
            return candidate;
        }

        return null;  // recurring but no schedule configured — shouldn't happen, treat as never due
    }
}
