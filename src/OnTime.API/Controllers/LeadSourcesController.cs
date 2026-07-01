using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Common;
using OnTime.Application.DTOs.LeadSources;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

[ApiController]
[Route("api/lead-sources")]
[Authorize]
public class LeadSourcesController : ControllerBase
{
    private readonly ILeadSourceService _leadSources;

    public LeadSourcesController(ILeadSourceService leadSources) => _leadSources = leadSources;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _leadSources.GetByUserAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadSourceRequest request, CancellationToken ct)
    {
        var result = await _leadSources.CreateAsync(User.GetUserId(), request, ct);
        return Created($"api/lead-sources/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadSourceRequest request, CancellationToken ct)
    {
        var result = await _leadSources.UpdateAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetLeadSourceActiveRequest request, CancellationToken ct)
    {
        await _leadSources.SetActiveAsync(id, User.GetUserId(), request.IsActive, ct);
        return NoContent();
    }
}
