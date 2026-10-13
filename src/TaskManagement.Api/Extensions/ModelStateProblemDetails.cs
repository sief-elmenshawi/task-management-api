using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskManagement.Api.Extensions;

public static class ModelStateProblemDetails
{
    public const string ValidationCode = "ValidationFailed";

    public static ProblemDetails Create(ModelStateDictionary modelState)
    {
        var errors = CollectErrors(modelState);
        var normalized = new Dictionary<string, string[]>();

        foreach (var (key, _) in errors)
        {
            var property = MapProperty(key);

            if (property is not null)
            {
                normalized[property] = [MessageFor(property)];
                continue;
            }

            var field = MapField(key);

            if (field is null)
            {
                normalized["request"] = [
                    string.IsNullOrWhiteSpace(key)
                        ? "A request body is required."
                        : "The request body is malformed or is not of the expected type."
                ];
            }
            else
            {
                normalized[field] = [$"The value of '{field}' is invalid."];
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
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? error.Exception?.Message
                    : error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!)
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

    private static string? MapField(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key == "$")
        {
            return null;
        }

        var field = key.StartsWith("$.", StringComparison.Ordinal)
            ? key[2..]
            : key;

        var lastSegment = field.Split('.')[^1];

        if (lastSegment.Length == 0 || lastSegment == "$")
        {
            return null;
        }

        return ToCamelCase(lastSegment);
    }

    private static string ToCamelCase(string value) =>
        char.IsUpper(value[0])
            ? string.Concat(char.ToLowerInvariant(value[0]), value[1..])
            : value;

    private static string MessageFor(string property) => property switch
    {
        "priority" => "Invalid value. Allowed values: Low, Medium, High.",
        "status" => "Invalid value. Allowed values: Pending, InProgress, Done.",
        "id" => "The id must be a valid GUID.",
        _ => "The value is invalid."
    };
}
