using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Brands;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

[ApiController]
[Route("api/brands")]
[Authorize(Policy = "ManagerOnly")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands) => _brands = brands;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _brands.GetByCompanyAsync(User.RequireCompanyId(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _brands.GetByIdAsync(id, User.RequireCompanyId(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken ct)
    {
        var result = await _brands.CreateAsync(User.RequireCompanyId(), request, ct);
        return Created($"api/brands/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken ct)
    {
        var result = await _brands.UpdateAsync(id, User.RequireCompanyId(), request, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetBrandActiveRequest request,
        CancellationToken ct)
    {
        await _brands.SetActiveAsync(id, User.RequireCompanyId(), request.IsActive, ct);
        return NoContent();
    }

    // ── Vehicle brands this Stand sells (see USER-BRANDS.md) ──────────────

    [HttpGet("{id:guid}/vehicle-brands")]
    public async Task<IActionResult> GetVehicleBrands(Guid id, CancellationToken ct)
    {
        var result = await _brands.GetVehicleBrandIdsAsync(id, User.RequireCompanyId(), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/vehicle-brands")]
    public async Task<IActionResult> SetVehicleBrands(
        Guid id,
        [FromBody] UpdateBrandVehicleBrandsRequest request,
        CancellationToken ct)
    {
        await _brands.SetVehicleBrandIdsAsync(id, User.RequireCompanyId(), request, ct);
        return NoContent();
    }

    // ── Membership grants (own company only) ────────────────────────────────

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> GrantMembership(
        Guid id,
        [FromBody] GrantMembershipRequest request,
        CancellationToken ct)
    {
        await _brands.GrantMembershipAsync(id, User.RequireCompanyId(), request.UserId, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RevokeMembership(Guid id, Guid userId, CancellationToken ct)
    {
        await _brands.RevokeMembershipAsync(id, User.RequireCompanyId(), userId, ct);
        return NoContent();
    }
}
