using FluentValidation;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Common;
using TaskManagement.Application.Users.Dtos;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<RegisterRequest> _registerValidator;

    public UserService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<RegisterRequest> registerValidator)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _registerValidator = registerValidator;
    }

    public async Task<Result<IReadOnlyList<UserAdminResponse>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _users.GetAllAsync(cancellationToken);
        IReadOnlyList<UserAdminResponse> response = users.Select(ToResponse).ToList();

        return Result.Success(response);
    }

    public async Task<Result<UserAdminResponse>> CreateAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<UserAdminResponse>(validation.ToError());
        }

        var email = EmailNormalizer.Normalize(request.Email);

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<UserAdminResponse>(
                Error.Conflict("A user with this email already exists.", "EmailAlreadyExists"));
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = new User(request.Name.Trim(), email, passwordHash, UserRole.User);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(user));
    }

    public async Task<Result> DeleteAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == userId)
        {
            return Result.Failure(
                Error.Forbidden("You cannot delete your own account.", "CannotDeleteSelf"));
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User not found.", "UserNotFound"));
        }

        var activeTokens = await _refreshTokens.GetActiveByUserIdAsync(userId, cancellationToken);

        user.MarkAsDeleted();
        _users.Update(user);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }

        if (activeTokens.Count > 0)
        {
            _refreshTokens.UpdateRange(activeTokens);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static UserAdminResponse ToResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Role.ToString(), user.CreatedAt);
}
