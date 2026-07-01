using OnTime.Application.Common;
using OnTime.Application.DTOs.Admin;

namespace OnTime.Application.Interfaces;

public interface IEmailLogService
{
    Task<PagedResult<EmailLogDto>> GetPagedAsync(
        int page, int pageSize, string? emailType = null, bool? success = null, CancellationToken ct = default);
}
