using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Notifications;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
        => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] NotificationFilterParams filter,
        CancellationToken ct)
    {
        var result = await _notifications.GetPagedAsync(User.GetUserId(), filter, ct);
        return Ok(result);
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetToday(CancellationToken ct)
    {
        var result = await _notifications.GetTodayAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("overdue-count")]
    public async Task<IActionResult> GetOverdueCount(CancellationToken ct)
    {
        var count = await _notifications.GetOverdueCountAsync(User.GetUserId(), ct);
        return Ok(count);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken ct)
    {
        var result = await _notifications.CreateAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/done")]
    public async Task<IActionResult> MarkDone(Guid id, CancellationToken ct)
    {
        await _notifications.MarkDoneAsync(id, User.GetUserId(), ct);
        return Ok();
    }

    [HttpPatch("{id:guid}/snooze")]
    public async Task<IActionResult> Snooze(
        Guid id,
        [FromBody] SnoozeNotificationRequest request,
        CancellationToken ct)
    {
        await _notifications.SnoozeAsync(id, User.GetUserId(), request, ct);
        return Ok();
    }

    [HttpPatch("{id:guid}/ignore")]
    public async Task<IActionResult> Ignore(Guid id, CancellationToken ct)
    {
        await _notifications.IgnoreAsync(id, User.GetUserId(), ct);
        return Ok();
    }
}
