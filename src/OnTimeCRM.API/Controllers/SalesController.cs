using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.Common;
using OnTimeCRM.Application.DTOs.Sales;
using OnTimeCRM.Application.Interfaces;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISaleService _sales;

    public SalesController(ISaleService sales) => _sales = sales;

    [HttpGet("sales")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] SaleFilterParams filter,
        CancellationToken ct)
    {
        var result = await _sales.GetPagedAsync(User.GetUserId(), filter, ct);
        return Ok(result);
    }

    [HttpGet("sales/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sales.GetByIdAsync(id, User.GetUserId(), ct);
        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _sales.GetDashboardAsync(User.GetUserId(), ct);
        return Ok(result);
    }
}
