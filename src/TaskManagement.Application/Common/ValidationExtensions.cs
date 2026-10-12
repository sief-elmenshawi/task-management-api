using FluentValidation.Results;

namespace TaskManagement.Application.Common;

public static class ValidationExtensions
{
    public static Error ToError(this ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return Error.Validation(errors);
    }
}
