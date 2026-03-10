using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Friends;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendshipService _service;

    public FriendsController(IFriendshipService service) => _service = service;

    /// <summary>Get all accepted friends</summary>
    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken ct)
    {
        var result = await _service.GetFriendsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>Get pending friend requests received by the current user</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests(CancellationToken ct)
    {
        var result = await _service.GetPendingRequestsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>Send a friend request by email</summary>
    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest(
        [FromBody] SendFriendRequestDto request, CancellationToken ct)
    {
        var result = await _service.SendRequestAsync(User.GetUserId(), request, ct);
        return Created($"api/friends/requests/{result.FriendshipId}", result);
    }

    /// <summary>Accept a pending friend request</summary>
    [HttpPatch("requests/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var result = await _service.AcceptAsync(User.GetUserId(), id, ct);
        return Ok(result);
    }

    /// <summary>Reject a pending friend request</summary>
    [HttpPatch("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        await _service.RejectAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    /// <summary>Remove an accepted friend</summary>
    [HttpDelete("{friendUserId:guid}")]
    public async Task<IActionResult> Remove(Guid friendUserId, CancellationToken ct)
    {
        await _service.RemoveAsync(User.GetUserId(), friendUserId, ct);
        return NoContent();
    }

    /// <summary>Get a friend's public profile and visible KPIs</summary>
    [HttpGet("{friendUserId:guid}/profile")]
    public async Task<IActionResult> GetProfile(Guid friendUserId, CancellationToken ct)
    {
        var result = await _service.GetFriendProfileAsync(User.GetUserId(), friendUserId, ct);
        return Ok(result);
    }

    /// <summary>Get current user's public profile visibility settings</summary>
    [HttpGet("profile/settings")]
    public async Task<IActionResult> GetMySettings(CancellationToken ct)
    {
        var result = await _service.GetMyPublicProfileAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>Update current user's public profile visibility settings</summary>
    [HttpPut("profile/settings")]
    public async Task<IActionResult> UpdateMySettings(
        [FromBody] PublicProfileSettingsDto request, CancellationToken ct)
    {
        var result = await _service.UpdateMyPublicProfileAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }
}
