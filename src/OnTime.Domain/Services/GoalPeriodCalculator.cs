using OnTime.Domain.Enums;

namespace OnTime.Domain.Services;

/// <summary>
/// Pure calculation: the [start, end) window a given GoalPeriod covers "as of" some instant.
/// Parametrized by <paramref name="asOf"/> (rather than always DateTimeOffset.UtcNow) so callers
/// can ask both "what's the live window right now" (UserGoalService) and "what was the window for
/// a moment in the past" (business-summary email, to report a just-completed cycle).
/// </summary>
public static class GoalPeriodCalculator
{
    public static (DateTimeOffset start, DateTimeOffset end) Window(GoalPeriod period, DateTimeOffset asOf)
    {
        // Build an explicit UTC-midnight DateTimeOffset rather than going through DateTime.Date —
        // .Date returns Kind=Unspecified, and the implicit conversion back to DateTimeOffset would
        // re-apply the machine's local timezone offset instead of UTC (Npgsql then rejects it).
        var dayUtc = new DateTimeOffset(asOf.Year, asOf.Month, asOf.Day, 0, 0, 0, TimeSpan.Zero);
        return period switch
        {
            GoalPeriod.Daily   => (dayUtc, dayUtc.AddDays(1)),
            GoalPeriod.Weekly  => (StartOfWeek(dayUtc), StartOfWeek(dayUtc).AddDays(7)),
            GoalPeriod.Annual  => (new DateTimeOffset(asOf.Year, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(asOf.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            _                  => (new DateTimeOffset(asOf.Year, asOf.Month, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(asOf.Year, asOf.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1)),
        };
    }

    /// <summary>Monday 00:00 UTC of the week containing <paramref name="dayUtc"/> (already UTC-midnight).</summary>
    public static DateTimeOffset StartOfWeek(DateTimeOffset dayUtc)
    {
        var diff = (7 + (dayUtc.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dayUtc.AddDays(-diff);
    }
}
