using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnTime.Application.Interfaces;

namespace OnTime.Infrastructure.Email;

/// <summary>
/// Sends transactional email via Brevo's REST API (https://api.brevo.com/v3/smtp/email).
/// No SDK dependency — a single JSON POST is all the endpoint needs.
/// </summary>
public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient http, IConfiguration config, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var apiKey = _config["Brevo:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Brevo:ApiKey not configured — skipping email to {ToEmail} ({Subject})", toEmail, subject);
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
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Brevo send failed ({Status}) to {ToEmail}: {Body}", response.StatusCode, toEmail, body);
        }
    }
}
