using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Clients;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clients;

    public ClientsController(IClientService clients) => _clients = clients;

    // ── List & Create ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ClientFilterParams filter,
        CancellationToken ct)
    {
        var brandId = User.IsManager() ? (Guid?)User.GetBrandId() : null;
        var result = await _clients.GetPagedAsync(User.GetUserId(), brandId, filter, ct);
        return Ok(result);
    }

    /// <summary>Atomic: creates a Client + first Proposal in one transaction</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateClientRequest request,
        CancellationToken ct)
    {
        var result = await _clients.CreateAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    // ── Hot deals ────────────────────────────────────────────────────────

    [HttpGet("hot")]
    public async Task<IActionResult> GetHotDeals(CancellationToken ct)
    {
        var result = await _clients.GetHotDealsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    // ── Single client ───────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _clients.GetByIdAsync(id, User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _clients.DeleteAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    // ── Stage change (the main CRM flow) ────────────────────────────────

    [HttpPut("{id:guid}/stage")]
    public async Task<IActionResult> UpdateStage(
        Guid id,
        [FromBody] UpdateClientStageRequest request,
        CancellationToken ct)
    {
        var result = await _clients.UpdateStageAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }

    // ── History ─────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var result = await _clients.GetHistoryAsync(id, User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/sales")]
    public async Task<IActionResult> GetSaleHistory(Guid id, CancellationToken ct)
    {
        var result = await _clients.GetSalesHistoryAsync(id, User.GetUserId(), ct);
        return Ok(result);
    }
}
