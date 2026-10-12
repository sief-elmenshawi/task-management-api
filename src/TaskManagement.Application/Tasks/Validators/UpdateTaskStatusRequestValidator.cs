using FluentValidation;
using TaskManagement.Application.Tasks.Dtos;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Validators;

public sealed class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status is required.")
            .Must(status => status is not null && Enum.IsDefined(typeof(TaskItemStatus), status.Value))
            .WithMessage("Status is invalid.");
    }
}
