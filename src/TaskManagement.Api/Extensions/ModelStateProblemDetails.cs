using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskManagement.Api.Extensions;

public static class ModelStateProblemDetails
{
    public const string ValidationCode = "ValidationFailed";

    public static ProblemDetails Create(ModelStateDictionary modelState)
    {
        var errors = CollectErrors(modelState);
        var hasPropertyError = errors.Keys.Any(key => MapProperty(key) is not null);

        var normalized = new Dictionary<string, string[]>();

        foreach (var (key, messages) in errors)
        {
            var property = MapProperty(key);

            if (property is not null)
            {
                normalized[property] = [MessageFor(property)];
            }
            else if (!hasPropertyError)
            {
                normalized["request"] = ["A request body is required."];
            }
        }

        if (normalized.Count == 0)
        {
            normalized["request"] = ["A request body is required."];
        }

        var detail = string.Join(
            " ",
            normalized.Values.SelectMany(value => value).Distinct());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://httpstatuses.io/400",
            Detail = detail
        };

        problemDetails.Extensions["code"] = ValidationCode;
        problemDetails.Extensions["errors"] = normalized;

        return problemDetails;
    }

    private static Dictionary<string, string[]> CollectErrors(ModelStateDictionary modelState)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var (key, entry) in modelState)
        {
            var messages = entry.Errors
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();

            if (messages.Length > 0)
            {
                errors[key] = messages;
            }
        }

        return errors;
    }

    private static string? MapProperty(string key)
    {
        if (key.Contains("priority", StringComparison.OrdinalIgnoreCase))
        {
            return "priority";
        }

        if (key.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return "status";
        }

        if (key.Equals("id", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".id", StringComparison.OrdinalIgnoreCase))
        {
            return "id";
        }

        return null;
    }

    private static string MessageFor(string property) => property switch
    {
        "priority" => "Invalid value. Allowed values: Low, Medium, High.",
        "status" => "Invalid value. Allowed values: Pending, InProgress, Done.",
        "id" => "The id must be a valid GUID.",
        _ => "The value is invalid."
    };
}
