using FarmMonitoring.Api.Contracts;
using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; do not attempt to write an error to the closed connection.
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, error) = exception switch
            {
                NotFoundException missing => (StatusCodes.Status404NotFound, ApiError.Create(context, missing.Message)),
                ConflictException conflict => (StatusCodes.Status409Conflict, ApiError.Create(context, conflict.Message)),
                AuthException auth => (StatusCodes.Status401Unauthorized, ApiError.Create(context, auth.Message)),
                ValidationException validation => (StatusCodes.Status400BadRequest,
                    ApiError.Create(context, "Validation failed.", validation.Errors
                        .Select(x => new ApiFieldError(x.PropertyName, x.ErrorMessage)).ToArray())),
                BadHttpRequestException request => (request.StatusCode, ApiError.Create(context, "Invalid request.")),
                _ => (StatusCodes.Status500InternalServerError, ApiError.Create(context, "An unexpected error occurred."))
            };
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Request failed. TraceId: {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
        }
    }
}
