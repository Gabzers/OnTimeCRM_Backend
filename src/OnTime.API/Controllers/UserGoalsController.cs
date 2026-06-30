using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Goals;
using OnTime.Application.Interfaces;

namespace OnTime.API.Controllers;

[ApiController]
[Route("api/goals")]
[Authorize]
public class UserGoalsController : ControllerBase
{
    private readonly IUserGoalService _goals;

    public UserGoalsController(IUserGoalService goals) => _goals = goals;

    [HttpGet]
    public async Task<IActionResult> GetGoals(CancellationToken ct)
    {
        var result = await _goals.GetGoalsAsync(User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal([FromBody] CreateUserGoalRequest request, CancellationToken ct)
    {
        var result = await _goals.CreateGoalAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetGoals), result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateGoal(Guid id, [FromBody] UpdateUserGoalRequest request, CancellationToken ct)
    {
        var result = await _goals.UpdateGoalAsync(User.GetUserId(), id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken ct)
    {
        await _goals.DeleteGoalAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderGoalsRequest request, CancellationToken ct)
    {
        await _goals.ReorderGoalsAsync(User.GetUserId(), request, ct);
        return NoContent();
    }
}
