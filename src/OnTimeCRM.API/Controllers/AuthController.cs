using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnTimeCRM.Application.DTOs.Auth;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Application.Interfaces.Repositories;

namespace OnTimeCRM.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService      _auth;
    private readonly IAuthRepository   _authRepo;

    public AuthController(IAuthService auth, IAuthRepository authRepo)
    {
        _auth     = auth;
        _authRepo = authRepo;
    }

    /// <summary>Register a new company + brand + manager (first-time onboarding)</summary>
    [HttpPost("register-manager")]
    public async Task<IActionResult> RegisterManager(
        [FromBody] RegisterManagerRequest request,
        CancellationToken ct)
    {
        var result = await _auth.RegisterManagerAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Register a salesperson under an existing brand</summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterSalesperson(
        [FromBody] RegisterSalespersonRequest request,
        CancellationToken ct)
    {
        var result = await _auth.RegisterSalespersonAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Login</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Logout (stateless JWT — client-side token discard)</summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => NoContent();

    /// <summary>Public list of active companies (used on registration screen)</summary>
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken ct)
    {
        var result = await _authRepo.GetCompanyListAsync(ct);
        return Ok(result);
    }

    /// <summary>Public list of active brands for a company (used on registration screen)</summary>
    [HttpGet("companies/{companyId:guid}/brands")]
    public async Task<IActionResult> GetBrandsByCompany(Guid companyId, CancellationToken ct)
    {
        var result = await _authRepo.GetBrandsByCompanyAsync(companyId, ct);
        return Ok(result);
    }
}
