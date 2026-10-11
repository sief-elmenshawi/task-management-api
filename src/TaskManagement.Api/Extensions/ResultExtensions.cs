using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Common;

namespace TaskManagement.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");
        }

        return CreateProblemResult(result.Error);
    }

    public static IActionResult ToProblemDetails<TValue>(this Result<TValue> result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");
        }

        return CreateProblemResult(result.Error);
    }

    private static ObjectResult CreateProblemResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(error.Type),
            Type = $"https://httpstatuses.io/{statusCode}",
            Detail = error.Message
        };

        problemDetails.Extensions["code"] = error.Code;

        if (error.ValidationErrors is { Count: > 0 })
        {
            problemDetails.Extensions["errors"] = error.ValidationErrors;
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string GetTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "One or more validation errors occurred.",
        ErrorType.Unauthorized => "Unauthorized.",
        ErrorType.Forbidden => "Forbidden.",
        ErrorType.NotFound => "Resource not found.",
        ErrorType.Conflict => "A conflict occurred.",
        _ => "An error occurred."
    };
}
