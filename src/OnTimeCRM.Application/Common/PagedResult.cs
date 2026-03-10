namespace OnTimeCRM.Application.Common;

public record PagedResult<T>(
    IEnumerable<T> Items,
    int Total,
    int Page,
    int PageSize);
