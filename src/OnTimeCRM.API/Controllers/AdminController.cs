using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Application.DTOs.Companies;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

/// <summary>
/// Manager-only admin panel for managing companies and their brands.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "ManagerOnly")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IBrandService _brands;

    public AdminController(IAdminService admin, IBrandService brands)
    {
        _admin  = admin;
        _brands = brands;
    }

    // ── Companies ─────────────────────────────────────────────────────────

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken ct)
    {
        var result = await _admin.GetCompaniesAsync(ct);
        return Ok(result);
    }

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany(
        [FromBody] CreateCompanyAdminRequest request, CancellationToken ct)
    {
        var result = await _admin.CreateCompanyAsync(request, ct);
        return Created($"api/admin/companies/{result.Id}", result);
    }

    [HttpPut("companies/{id:guid}")]
    public async Task<IActionResult> UpdateCompany(
        Guid id, [FromBody] UpdateCompanyAdminRequest request, CancellationToken ct)
    {
        var result = await _admin.UpdateCompanyAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPatch("companies/{id:guid}/active")]
    public async Task<IActionResult> SetCompanyActive(
        Guid id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await _admin.SetCompanyActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ── Brands (per company) ─────────────────────────────────────────────

    [HttpGet("companies/{companyId:guid}/brands")]
    public async Task<IActionResult> GetBrands(Guid companyId, CancellationToken ct)
    {
        var result = await _brands.GetByCompanyAsync(companyId, ct);
        return Ok(result);
    }

    [HttpPost("companies/{companyId:guid}/brands")]
    public async Task<IActionResult> CreateBrand(
        Guid companyId, [FromBody] CreateBrandRequest request, CancellationToken ct)
    {
        var result = await _brands.CreateAsync(companyId, request, ct);
        return Created($"api/admin/companies/{companyId}/brands/{result.Id}", result);
    }

    [HttpPut("companies/{companyId:guid}/brands/{id:guid}")]
    public async Task<IActionResult> UpdateBrand(
        Guid companyId, Guid id, [FromBody] UpdateBrandRequest request, CancellationToken ct)
    {
        var result = await _brands.UpdateAsync(id, companyId, request, ct);
        return Ok(result);
    }

    [HttpPatch("companies/{companyId:guid}/brands/{id:guid}/active")]
    public async Task<IActionResult> SetBrandActive(
        Guid companyId, Guid id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await _brands.SetActiveAsync(id, companyId, request.IsActive, ct);
        return NoContent();
    }
}

public record SetActiveRequest(bool IsActive);
