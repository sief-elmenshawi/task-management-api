using TaskManagement.Api.Extensions;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                exception,
                "The response has already started; the exception cannot be converted to ProblemDetails. {Path}",
                context.Request.Path);

            return;
        }

        var (statusCode, title, code) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Handled {ExceptionType} while processing {Method} {Path}.",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path);
        }

        var detail = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;

        await context.Response.WriteProblemDetailsAsync(statusCode, title, detail, code);
    }

    private static (int StatusCode, string Title, string Code) MapException(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found.", "NotFound"),
        ConflictException => (StatusCodes.Status409Conflict, "A conflict occurred.", "Conflict"),
        ConcurrencyException => (StatusCodes.Status409Conflict, "A conflict occurred.", "ConcurrencyConflict"),
        DuplicateKeyException => (StatusCodes.Status409Conflict, "A conflict occurred.", "DuplicateKey"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden.", "Forbidden"),
        DomainException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", "DomainRuleViolation"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "InternalServerError")
    };
}
