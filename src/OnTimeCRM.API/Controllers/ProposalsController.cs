using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Proposals;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Authorize]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposals;

    public ProposalsController(IProposalService proposals) => _proposals = proposals;

    // ── List ────────────────────────────────────────────────────────────

    [HttpGet("api/proposals")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ProposalFilterParams filter,
        CancellationToken ct)
    {
        var result = await _proposals.GetPagedAsync(User.GetUserId(), filter, ct);
        return Ok(result);
    }

    // ── Detail & Update (by proposal id) ────────────────────────────────

    [HttpGet("api/proposals/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _proposals.GetByIdAsync(id, User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPut("api/proposals/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateProposalRequest request,
        CancellationToken ct)
    {
        var result = await _proposals.UpdateAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }

    // ── Create nested under client ────────────────────────────────────

    [HttpPost("api/clients/{clientId:guid}/proposals")]
    public async Task<IActionResult> CreateForClient(
        Guid clientId,
        [FromBody] CreateProposalRequest request,
        CancellationToken ct)
    {
        var result = await _proposals.CreateForClientAsync(clientId, User.GetUserId(), request, ct);
        return Ok(result);
    }

    // ── Actions ──────────────────────────────────────────────────────────

    [HttpPost("api/proposals/{id:guid}/lost")]
    public async Task<IActionResult> MarkLost(
        Guid id,
        [FromBody] MarkProposalLostRequest request,
        CancellationToken ct)
    {
        var result = await _proposals.MarkLostAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }

    /// <summary>Convert proposal to sale — SoldAt must be supplied by the user, never auto-set.</summary>
    [HttpPost("api/proposals/{id:guid}/convert")]
    public async Task<IActionResult> ConvertToSale(
        Guid id,
        [FromBody] ConvertToSaleRequest request,
        CancellationToken ct)
    {
        var result = await _proposals.ConvertToSaleAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }
}
