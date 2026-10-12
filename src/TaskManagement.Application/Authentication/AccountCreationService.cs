using FluentValidation;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Application.Authentication;

internal static class AccountCreationService
{
    public static async Task<Result<User>> CreateUserAsync(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<RegisterRequest> validator,
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<User>(validation.ToError());
        }

        var email = EmailNormalizer.Normalize(request.Email);

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<User>(EmailAlreadyExists());
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = new User(request.Name.Trim(), email, passwordHash, UserRole.User);

        await users.AddAsync(user, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return Result.Failure<User>(EmailAlreadyExists());
        }

        return Result.Success(user);
    }

    private static Error EmailAlreadyExists() =>
        Error.Conflict("A user with this email already exists.", "EmailAlreadyExists");
}
