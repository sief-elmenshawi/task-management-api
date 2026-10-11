using FluentValidation;
using TaskManagement.Application.Tasks.Dtos;

namespace TaskManagement.Application.Tasks.Validators;

public sealed class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status is invalid.");
    }
}
