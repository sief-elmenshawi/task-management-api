using System.Text;
using FluentValidation;
using TaskManagement.Application.Authentication.Dtos;

namespace TaskManagement.Application.Authentication.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Must(password => Encoding.UTF8.GetByteCount(password) <= 72)
            .WithMessage("Password must not exceed 72 UTF-8 bytes (BCrypt limit).");
    }
}
