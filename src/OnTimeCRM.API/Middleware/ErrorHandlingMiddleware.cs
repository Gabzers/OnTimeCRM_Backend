using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnTimeCRM.Application.Common;

namespace OnTimeCRM.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            await WriteErrorAsync(context, ex.Error.StatusCode, ex.Error.Code,
                ex.Error.Message, ex.Error.Class, ex.Details);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await WriteErrorAsync(context, 409, "CONFLICT",
                "A record with these values already exists.", "Conflict", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, "INTERNAL_ERROR",
                "An unexpected error occurred.", "InternalError", null);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Check inner exception message for Postgres error code 23505
        return ex.InnerException?.Message?.Contains("23505") == true
            || ex.InnerException?.GetType().Name == "PostgresException"
               && (ex.InnerException.Message?.Contains("unique") == true
                   || ex.InnerException.Message?.Contains("23505") == true);
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int status,
        string code,
        string message,
        string errorClass,
        string? details)
    {
        context.Response.StatusCode  = status;
        context.Response.ContentType = "application/json";

        var traceId = context.TraceIdentifier;

        var body = JsonSerializer.Serialize(new
        {
            code,
            message,
            @class   = errorClass,
            details,
            traceId
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await context.Response.WriteAsync(body);
    }
}
