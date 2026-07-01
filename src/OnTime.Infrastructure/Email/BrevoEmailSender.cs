using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnTime.Application.Interfaces;
using OnTime.Domain.Entities;

namespace OnTime.Infrastructure.Email;

/// <summary>
/// Sends transactional email via Brevo's REST API (https://api.brevo.com/v3/smtp/email).
/// No SDK dependency — a single JSON POST is all the endpoint needs. Also persists one EmailLog
/// row per attempt (success or failure) — see EmailLog for why: Brevo's own dashboard was
/// previously the only place a sent email was visible at all.
/// </summary>
public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<BrevoEmailSender> _logger;
    private readonly IAppDbContext _db;

    public BrevoEmailSender(HttpClient http, IConfiguration config, ILogger<BrevoEmailSender> logger, IAppDbContext db)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _db = db;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string emailType, CancellationToken ct = default)
    {
        var apiKey = _config["Brevo:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Brevo:ApiKey not configured — skipping email to {ToEmail} ({Subject})", toEmail, subject);
            await LogAsync(toEmail, subject, emailType, success: false, error: "Brevo:ApiKey not configured", ct);
            return;
        }

        var senderEmail = _config["Brevo:SenderEmail"];
        var senderName = _config["Brevo:SenderName"] ?? "OnTime";

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = senderName, email = senderEmail },
                to = new[] { new { email = toEmail, name = toName } },
                subject,
                htmlContent = htmlBody
            })
        };
        request.Headers.Add("api-key", apiKey);
        request.Headers.Add("Accept", "application/json");

        var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            await LogAsync(toEmail, subject, emailType, success: true, error: null, ct);
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Brevo send failed ({Status}) to {ToEmail}: {Body}", response.StatusCode, toEmail, body);
            await LogAsync(toEmail, subject, emailType, success: false, error: $"{(int)response.StatusCode}: {body}", ct);
        }
    }

    private async Task LogAsync(string toEmail, string subject, string emailType, bool success, string? error, CancellationToken ct)
    {
        _db.EmailLogs.Add(new EmailLog
        {
            ToEmail      = toEmail,
            Subject      = subject,
            EmailType    = emailType,
            Success      = success,
            ErrorMessage = error
        });
        await _db.SaveChangesAsync(ct);
    }
}
