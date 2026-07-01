using Microsoft.EntityFrameworkCore;
using OnTime.Application.Common;
using OnTime.Application.DTOs.Admin;
using OnTime.Application.Interfaces;

namespace OnTime.Application.Services;

public class EmailLogService : IEmailLogService
{
    private readonly IAppDbContext _db;

    public EmailLogService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<EmailLogDto>> GetPagedAsync(
        int page, int pageSize, string? emailType = null, bool? success = null, CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize, 1, 100);
        var query = _db.EmailLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(emailType))
            query = query.Where(e => e.EmailType == emailType);
        if (success.HasValue)
            query = query.Where(e => e.Success == success.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(e => new EmailLogDto(
                e.Id, e.ToEmail, e.Subject, e.EmailType, e.Success, e.ErrorMessage, e.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<EmailLogDto>(items, total, page, size);
    }
}
