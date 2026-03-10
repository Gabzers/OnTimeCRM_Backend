using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Stages;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/stages")]
[Authorize]
public class StagesController : ControllerBase
{
    private readonly IClientStageService _stages;

    public StagesController(IClientStageService stages) => _stages = stages;

    // ── Stages CRUD ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _stages.GetByUserAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStageRequest request,
        CancellationToken ct)
    {
        var result = await _stages.CreateAsync(User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStageRequest request,
        CancellationToken ct)
    {
        var result = await _stages.UpdateAsync(id, User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _stages.DeleteAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(
        [FromBody] ReorderStagesRequest request,
        CancellationToken ct)
    {
        await _stages.ReorderAsync(User.GetUserId(), request, ct);
        return Ok();
    }

    // ── Stage notification templates ─────────────────────────────────────

    [HttpPost("{stageId:guid}/templates")]
    public async Task<IActionResult> AddTemplate(
        Guid stageId,
        [FromBody] CreateStageTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _stages.AddTemplateAsync(stageId, User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpPut("{stageId:guid}/templates/{templateId:guid}")]
    public async Task<IActionResult> UpdateTemplate(
        Guid stageId,
        Guid templateId,
        [FromBody] UpdateStageTemplateRequest request,
        CancellationToken ct)
    {
        var result = await _stages.UpdateTemplateAsync(stageId, templateId, User.GetUserId(), request, ct);
        return Ok(result);
    }

    [HttpDelete("{stageId:guid}/templates/{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplate(
        Guid stageId,
        Guid templateId,
        CancellationToken ct)
    {
        await _stages.DeleteTemplateAsync(stageId, templateId, User.GetUserId(), ct);
        return NoContent();
    }
}
