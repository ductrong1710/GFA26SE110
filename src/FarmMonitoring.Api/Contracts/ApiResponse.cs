namespace FarmMonitoring.Api.Contracts;

public sealed record ApiResponse<T>(bool Success, T Data, string? Message = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
}

public sealed record ApiFieldError(string Field, string Message);
public sealed record ApiError(bool Success, string Message, IReadOnlyList<ApiFieldError> Errors, string TraceId)
{
    public static ApiError Create(HttpContext context, string message, IReadOnlyList<ApiFieldError>? errors = null) =>
        new(false, message, errors ?? [], context.TraceIdentifier);
}
