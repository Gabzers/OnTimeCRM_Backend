using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Subscription;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscription;

    public SubscriptionController(ISubscriptionService subscription)
        => _subscription = subscription;

    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var result = await _subscription.GetStatusAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(CancellationToken ct)
    {
        var result = await _subscription.GetPaymentsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(
        [FromBody] InitiateSubscriptionRequest request,
        CancellationToken ct)
    {
        var result = await _subscription.InitiateAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpGet("payments/{paymentId:guid}/status")]
    public async Task<IActionResult> GetPaymentStatus(Guid paymentId, CancellationToken ct)
    {
        var result = await _subscription.GetPaymentStatusAsync(paymentId, User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        await _subscription.CancelAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
