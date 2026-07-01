using OnTime.Domain.Common;

namespace OnTime.Domain.Entities;

/// <summary>Lightweight record of every email OnTime has attempted to send via Brevo — one row
/// per SendAsync call, success or failure, written by BrevoEmailSender itself. CreatedAt (from
/// BaseEntity) is when it was sent. Exists because Brevo's own dashboard was previously the only
/// place this was visible — see SECURITY.md 2026-07-01.</summary>
public class EmailLog : BaseEntity
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    /// <summary>"FriendRequest" | "Digest" | "BusinessSummary" — free-text, not an enum, since
    /// this is a log label, not a value anything branches on.</summary>
    public string EmailType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
