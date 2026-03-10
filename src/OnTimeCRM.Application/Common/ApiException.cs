namespace OnTimeCRM.Application.Common;

public class ApiException : Exception
{
    public ApiError Error { get; }
    public string? Details { get; }

    public ApiException(ApiError error, string? details = null)
        : base(error.Message)
    {
        Error = error;
        Details = details;
    }
}

public record ApiError(string Code, string Message, string Class, int StatusCode);
