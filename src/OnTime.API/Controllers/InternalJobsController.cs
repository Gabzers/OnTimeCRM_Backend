using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

/// <summary>
/// Not user-facing — called by the Supabase pg_cron job (via pg_net) on a schedule. Guarded by a
/// shared secret header instead of JWT auth, since there is no logged-in user behind this call.
/// See 04-DECISIONS/2026-06-30-stage-driven-temperature-and-notifications.md §4.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalJobsController : ControllerBase
{
    private readonly IScheduledJobsService _jobs;
    private readonly IConfiguration _config;

    public InternalJobsController(IScheduledJobsService jobs, IConfiguration config)
    {
        _jobs = jobs;
        _config = config;
    }

    [HttpPost("run-scheduled-jobs")]
    public async Task<IActionResult> RunScheduledJobs(CancellationToken ct)
    {
        var expectedKey = _config["InternalJobs:SecretKey"];
        if (string.IsNullOrEmpty(expectedKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "InternalJobs:SecretKey not configured.");

        if (!Request.Headers.TryGetValue("X-Internal-Key", out var providedKey) ||
            providedKey != expectedKey)
            return Unauthorized();

        var result = await _jobs.RunAsync(ct);
        return Ok(result);
    }
}
