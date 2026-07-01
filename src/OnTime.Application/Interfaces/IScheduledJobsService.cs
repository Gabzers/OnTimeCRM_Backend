namespace OnTime.Application.Interfaces;

public interface IScheduledJobsService
{
    /// <summary>Runs every job the pg_cron job is responsible for: stage-driven temperature
    /// transitions and due recurring-notification occurrences. Idempotent and safe to call more
    /// often than strictly necessary — each pass only touches rows that are actually due.</summary>
    Task<ScheduledJobsRunResult> RunAsync(CancellationToken ct = default);
}

public record ScheduledJobsRunResult(int TemperatureTransitions, int NotificationsGenerated, int SeriesCompleted, int DigestEmailsSent, int BusinessSummariesSent);
