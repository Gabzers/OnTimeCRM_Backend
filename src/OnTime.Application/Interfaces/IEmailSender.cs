namespace OnTime.Application.Interfaces;

public interface IEmailSender
{
    /// <summary>emailType is a free-text log label ("FriendRequest"/"Digest"/"BusinessSummary")
    /// persisted alongside the send attempt — see Domain.Entities.EmailLog.</summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string emailType, CancellationToken ct = default);
}
