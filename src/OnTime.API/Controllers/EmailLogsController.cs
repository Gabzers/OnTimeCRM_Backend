using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

/// <summary>
/// Platform-admin read access to every email OnTime has attempted to send (see EmailLog,
/// written by BrevoEmailSender). AdminOnly for the same reason as ErrorLogsController: this is
/// cross-tenant data, not something a customer's Manager should be able to browse.
/// </summary>
[ApiController]
[Route("api/admin/email-logs")]
[Authorize(Policy = "AdminOnly")]
public class EmailLogsController : ControllerBase
{
    private readonly IEmailLogService _emailLogs;

    public EmailLogsController(IEmailLogService emailLogs) => _emailLogs = emailLogs;

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? emailType = null,
        [FromQuery] bool? success = null,
        CancellationToken ct = default)
    {
        var result = await _emailLogs.GetPagedAsync(page, pageSize, emailType, success, ct);
        return Ok(result);
    }
}
