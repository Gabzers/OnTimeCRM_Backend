using System.Collections.Concurrent;
using OnTime.Application.Interfaces;

namespace OnTime.Tests.Infrastructure;

/// <summary>In-memory stand-in for the real Brevo sender — captures every call instead of hitting
/// the network, so tests can assert on subject/body/language without any external dependency.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public sealed record SentEmail(string ToEmail, string ToName, string Subject, string HtmlBody);

    private readonly ConcurrentBag<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> Sent => _sent.ToList();

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        _sent.Add(new SentEmail(toEmail, toName, subject, htmlBody));
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();
}
