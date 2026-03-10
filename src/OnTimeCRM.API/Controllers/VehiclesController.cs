using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.DTOs.Vehicles;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles) => _vehicles = vehicles;

    // ── Brands ────────────────────────────────────────────────────────────

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands(CancellationToken ct)
    {
        var result = await _vehicles.GetBrandsAsync(ct);
        return Ok(result);
    }

    [HttpPost("brands")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> CreateBrand(
        [FromBody] CreateVehicleBrandRequest request,
        CancellationToken ct)
    {
        var result = await _vehicles.CreateBrandAsync(request, ct);
        return Created($"api/vehicles/brands/{result.Id}", result);
    }

    [HttpDelete("brands/{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> DeleteBrand(Guid id, CancellationToken ct)
    {
        await _vehicles.DeleteBrandAsync(id, ct);
        return NoContent();
    }

    // ── Models ────────────────────────────────────────────────────────────

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(
        [FromQuery] VehicleSearchParams p,
        CancellationToken ct)
    {
        var result = await _vehicles.GetModelsAsync(p, ct);
        return Ok(result);
    }

    [HttpPost("models")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> CreateModel(
        [FromBody] CreateVehicleModelRequest request,
        CancellationToken ct)
    {
        var result = await _vehicles.CreateModelAsync(request, ct);
        return Created($"api/vehicles/models/{result.Id}", result);
    }

    [HttpPut("models/{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> UpdateModel(
        Guid id,
        [FromBody] UpdateVehicleModelRequest request,
        CancellationToken ct)
    {
        var result = await _vehicles.UpdateModelAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("models/{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> DeleteModel(Guid id, CancellationToken ct)
    {
        await _vehicles.DeleteModelAsync(id, ct);
        return NoContent();
    }

    // ── Versions ──────────────────────────────────────────────────────────

    [HttpGet("models/{modelId:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid modelId, CancellationToken ct)
    {
        var result = await _vehicles.GetVersionsAsync(modelId, ct);
        return Ok(result);
    }

    [HttpPost("models/{modelId:guid}/versions")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> CreateVersion(
        Guid modelId,
        [FromBody] CreateVehicleVersionRequest request,
        CancellationToken ct)
    {
        var result = await _vehicles.CreateVersionAsync(modelId, request, ct);
        return Created($"api/vehicles/models/{modelId}/versions/{result.Id}", result);
    }

    [HttpPut("models/{modelId:guid}/versions/{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> UpdateVersion(
        Guid modelId, Guid id,
        [FromBody] UpdateVehicleVersionRequest request,
        CancellationToken ct)
    {
        var result = await _vehicles.UpdateVersionAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("models/{modelId:guid}/versions/{id:guid}")]
    [Authorize(Policy = "ManagerOnly")]
    public async Task<IActionResult> DeleteVersion(Guid modelId, Guid id, CancellationToken ct)
    {
        await _vehicles.DeleteVersionAsync(id, ct);
        return NoContent();
    }
}

