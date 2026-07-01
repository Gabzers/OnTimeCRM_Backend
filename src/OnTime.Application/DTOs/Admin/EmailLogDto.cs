namespace OnTime.Application.DTOs.Admin;

public record EmailLogDto(
    Guid Id,
    string ToEmail,
    string Subject,
    string EmailType,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset CreatedAt
);
