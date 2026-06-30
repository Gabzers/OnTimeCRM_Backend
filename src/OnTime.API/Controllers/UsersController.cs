using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Auth;
using OnTime.Application.DTOs.Friends;
using OnTime.Application.DTOs.Users;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IAuthService _auth;
    private readonly ISaleService _sales;
    private readonly IFriendshipService _friendships;

    public UsersController(IUserService users, IAuthService auth, ISaleService sales, IFriendshipService friendships)
    {
        _users       = users;
        _auth        = auth;
        _sales       = sales;
        _friendships = friendships;
    }

    // ── Current user ────────────────────────────────────────────────────────

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var result = await _users.GetMeAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        var result = await _users.UpdateMeAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    /// <summary>The Stands (companies/brands) the calling user belongs to — see switch-brand.</summary>
    [HttpGet("me/memberships")]
    public async Task<IActionResult> GetMyMemberships(CancellationToken ct)
    {
        var result = await _auth.GetMyMembershipsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>Switches the calling user's active Stand (must already have membership) and
    /// returns a freshly minted JWT scoped to it — same shape as login.</summary>
    [HttpPost("me/switch-brand")]
    public async Task<IActionResult> SwitchBrand(
        [FromBody] SwitchBrandRequest request,
        CancellationToken ct)
    {
        var result = await _auth.SwitchBrandAsync(User.GetUserId(), request.BrandId, ct);
        return Ok(result);
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        await _users.ChangePasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpGet("me/public-profile")]
    public async Task<IActionResult> GetPublicProfile(CancellationToken ct)
    {
        var result = await _friendships.GetMyPublicProfileAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPut("me/public-profile")]
    public async Task<IActionResult> UpdatePublicProfile(
        [FromBody] PublicProfileSettingsDto dto,
        CancellationToken ct)
    {
        var result = await _friendships.UpdateMyPublicProfileAsync(User.GetUserId(), dto, ct);
        return Ok(result);
    }

    // ── Manager only ─────────────────────────────────────────────────────────

    /// <summary>List all salespeople in the manager's brand</summary>
    [HttpGet]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetByBrand(CancellationToken ct)
    {
        var result  = await _users.GetByBrandAsync(User.RequireBrandId(), ct);
        return Ok(result);
    }

    /// <summary>Get a specific salesperson (must belong to manager's brand)</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result  = await _users.GetByIdAsync(id, User.RequireBrandId(), ct);
        return Ok(result);
    }

    /// <summary>Invite / create a salesperson under the manager's brand</summary>
    [HttpPost]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> InviteSalesperson(
        [FromBody] RegisterSalespersonRequest request,
        CancellationToken ct)
    {
        // Ensure the manager can only create users in their own brand
        request = request with { BrandId = User.RequireBrandId(), CompanyId = User.RequireCompanyId() };
        var result = await _auth.RegisterSalespersonAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Activate / deactivate a salesperson</summary>
    [HttpPut("{id:guid}/active")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetUserActiveRequest request,
        CancellationToken ct)
    {
        var result  = await _users.SetActiveAsync(id, User.RequireBrandId(), request, ct);
        return Ok(result);
    }

    /// <summary>View the dashboard of a specific salesperson</summary>
    [HttpGet("{id:guid}/dashboard")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> GetUserDashboard(Guid id, CancellationToken ct)
    {
        await _users.GetByIdAsync(id, User.RequireBrandId(), ct);
        var result = await _sales.GetDashboardAsync(id, ct);
        return Ok(result);
    }
}
