namespace OnTime.Domain.Services;

/// <summary>
/// Pure calculation: given how long a client has been in a stage and that stage's ordered
/// temperature rules, what's the client's effective temperature right now?
/// </summary>
public static class StageTemperatureCalculator
{
    /// <param name="daysSinceEntry">Days elapsed since the client entered the stage.</param>
    /// <param name="rules">All rules for the stage, any order.</param>
    /// <returns>The temperature of the rule with the largest DaysAfterEntry not exceeding
    /// daysSinceEntry, or null if no rule applies yet (e.g. rules start at day 1, client just entered).</returns>
    public static int? EffectiveTemperature(double daysSinceEntry, IEnumerable<(int DaysAfterEntry, int Temperature)> rules)
    {
        var applicable = rules
            .Where(r => r.DaysAfterEntry <= daysSinceEntry)
            .OrderByDescending(r => r.DaysAfterEntry)
            .ToList();

        return applicable.Count == 0 ? null : applicable[0].Temperature;
    }
}
