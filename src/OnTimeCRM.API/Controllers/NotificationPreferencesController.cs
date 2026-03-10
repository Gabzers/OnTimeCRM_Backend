using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/preferences/notifications")]
[Authorize]
public class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _prefs;

    public NotificationPreferencesController(INotificationPreferenceService prefs)
        => _prefs = prefs;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _prefs.GetAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNotificationPreferenceRequest request,
        CancellationToken ct)
    {
        var result = await _prefs.UpdateAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }
}
