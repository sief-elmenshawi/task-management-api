using Microsoft.AspNetCore.Mvc;

namespace TaskManagement.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static Task WriteProblemDetailsAsync(
        this HttpResponse response,
        int statusCode,
        string title,
        string detail,
        string? code = null)
    {
        response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.io/{statusCode}",
            Detail = detail
        };

        if (code is not null)
        {
            problemDetails.Extensions["code"] = code;
        }

        return response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json");
    }
}
