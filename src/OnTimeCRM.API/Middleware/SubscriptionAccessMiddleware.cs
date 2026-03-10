using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Interfaces;
using OnTimeCRM.Domain.Enums;

namespace OnTimeCRM.API.Middleware;

/// <summary>
/// Enforces subscription / account access rules on every authenticated request.
/// Skips: /api/auth/*, /api/i18n, /api/webhooks/*, /api/subscription/*, /health
/// </summary>
public class SubscriptionAccessMiddleware
{
    private readonly RequestDelegate       _next;
    private readonly IServiceScopeFactory  _scopeFactory;

    private static readonly string[] _bypassPrefixes =
    [
        "/api/auth/",
        "/api/i18n",
        "/api/webhooks/",
        "/api/subscription/",
        "/health"
    ];

    public SubscriptionAccessMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next         = next;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip exempt routes
        if (_bypassPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Only check authenticated requests
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        // Admin role (2) bypasses all subscription enforcement
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        if (roleClaim == "2")
        {
            await _next(context);
            return;
        }

        UserAccountStatus status;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var dbUser = await db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.AccountStatus })
                .FirstOrDefaultAsync(u => u.Id == userId, context.RequestAborted);

            if (dbUser is null)
            {
                // JWT references a user that no longer exists in the DB (e.g. after DB wipe).
                await WriteBlockAsync(context, 401, "AUTH_UNAUTHORIZED", "User session is no longer valid. Please log in again.");
                return;
            }

            status = dbUser.AccountStatus;
        }

        var method = context.Request.Method;

        switch (status)
        {
            case UserAccountStatus.Active:
                await _next(context);
                return;

            case UserAccountStatus.Expired:
                // Read-only: GETs pass, mutations blocked
                if (HttpMethods.IsGet(method))
                {
                    await _next(context);
                    return;
                }
                await WriteBlockAsync(context, 402, "SUBSCRIPTION_EXPIRED", "Subscription has expired.");
                return;

            case UserAccountStatus.PendingActivation:
            case UserAccountStatus.Suspended:
                await WriteBlockAsync(context, 402, "SUBSCRIPTION_REQUIRED", "Active subscription required.");
                return;

            case UserAccountStatus.Cancelled:
            case UserAccountStatus.Inactive:
                await WriteBlockAsync(context, 403, "AUTH_FORBIDDEN", "Account is inactive or cancelled.");
                return;

            default:
                await _next(context);
                return;
        }
    }

    private static async Task WriteBlockAsync(
        HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode  = status;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { code, message, @class = code },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(body);
    }
}
