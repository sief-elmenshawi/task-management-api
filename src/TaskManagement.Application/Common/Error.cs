namespace TaskManagement.Application.Common;

public sealed class Error
{
    public static readonly Error None = new("None", string.Empty, ErrorType.None, null);

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    private Error(
        string code,
        string message,
        ErrorType type,
        IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        Code = code;
        Message = message;
        Type = type;
        ValidationErrors = validationErrors;
    }

    public static Error Validation(string message, string code = "ValidationFailed") =>
        new(code, message, ErrorType.Validation, null);

    public static Error Validation(IReadOnlyDictionary<string, string[]> errors, string code = "ValidationFailed")
    {
        var message = string.Join(" ", errors.Values.SelectMany(v => v));
        return new Error(code, message, ErrorType.Validation, errors);
    }

    public static Error Unauthorized(string message, string code = "Unauthorized") =>
        new(code, message, ErrorType.Unauthorized, null);

    public static Error Forbidden(string message, string code = "Forbidden") =>
        new(code, message, ErrorType.Forbidden, null);

    public static Error NotFound(string message, string code = "NotFound") =>
        new(code, message, ErrorType.NotFound, null);

    public static Error Conflict(string message, string code = "Conflict") =>
        new(code, message, ErrorType.Conflict, null);
}
