using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Brands;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

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
        var result = await _brands.GetByCompanyAsync(User.TryGetCompanyId() ?? Guid.Empty, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _brands.GetByIdAsync(id, User.TryGetCompanyId() ?? Guid.Empty, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken ct)
    {
        var result = await _brands.CreateAsync(User.TryGetCompanyId() ?? Guid.Empty, request, ct);
        return Created($"api/brands/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken ct)
    {
        var result = await _brands.UpdateAsync(id, User.TryGetCompanyId() ?? Guid.Empty, request, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetBrandActiveRequest request,
        CancellationToken ct)
    {
        await _brands.SetActiveAsync(id, User.TryGetCompanyId() ?? Guid.Empty, request.IsActive, ct);
        return NoContent();
    }
}
