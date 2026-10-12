using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Application.Authentication;

public sealed class AuthService : IAuthService
{
    private const int RefreshTokenExpiryDays = 7;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<UserResponse>(validation.ToError());
        }

        var email = EmailNormalizer.Normalize(request.Email);

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<UserResponse>(
                Error.Conflict("A user with this email already exists.", "EmailAlreadyExists"));
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = new User(request.Name.Trim(), email, passwordHash, UserRole.User);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered user {UserId}.", user.Id);

        return Result.Success(ToUserResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AuthResponse>(validation.ToError());
        }

        var email = EmailNormalizer.Normalize(request.Email);
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Email}.", email);

            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Invalid email or password.", "InvalidCredentials"));
        }

        _logger.LogInformation("User {UserId} logged in.", user.Id);

        var response = await IssueTokensAsync(user, cancellationToken);
        return Result.Success(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure<AuthResponse>(
                Error.Validation("Refresh token is required.", "RefreshTokenRequired"));
        }

        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await _refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (storedToken is null)
        {
            _logger.LogDebug("Refresh token was not recognized.");

            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Invalid refresh token.", "InvalidRefreshToken"));
        }

        if (storedToken.IsRevoked)
        {
            await RevokeAllActiveTokensAsync(storedToken.UserId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking all active sessions.",
                storedToken.UserId);

            return Result.Failure<AuthResponse>(
                Error.Unauthorized(
                    "Refresh token has been revoked. All active sessions have been terminated.",
                    "RefreshTokenReuseDetected"));
        }

        if (storedToken.IsExpired)
        {
            _logger.LogDebug("Expired refresh token presented for user {UserId}.", storedToken.UserId);

            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Refresh token has expired.", "RefreshTokenExpired"));
        }

        var user = await _users.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null)
        {
            _logger.LogDebug("Refresh token belongs to an unknown user {UserId}.", storedToken.UserId);

            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Invalid refresh token.", "InvalidRefreshToken"));
        }

        var newRefreshTokenValue = GenerateTokenValue();
        var newRefreshTokenHash = HashToken(newRefreshTokenValue);

        storedToken.Revoke(newRefreshTokenHash);

        var accessToken = _jwtTokenService.CreateAccessToken(user);
        var refreshToken = new RefreshToken(
            user.Id,
            newRefreshTokenHash,
            DateTime.UtcNow.AddDays(RefreshTokenExpiryDays));

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Rotated refresh token for user {UserId}.", user.Id);

        return Result.Success(new AuthResponse(accessToken.Token, newRefreshTokenValue, accessToken.ExpiresAt));
    }

    public async Task<Result> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure(
                Error.Validation("Refresh token is required.", "RefreshTokenRequired"));
        }

        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await _refreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (storedToken is null)
        {
            _logger.LogDebug("Logout attempted with an unrecognized refresh token.");

            return Result.Failure(
                Error.Unauthorized("Invalid refresh token.", "InvalidRefreshToken"));
        }

        if (!storedToken.IsRevoked)
        {
            storedToken.Revoke();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Revoked refresh token for user {UserId}.", storedToken.UserId);
        }

        return Result.Success();
    }

    public async Task<Result<UserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("User not found.", "UserNotFound"));
        }

        return Result.Success(ToUserResponse(user));
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenService.CreateAccessToken(user);

        var refreshTokenValue = GenerateTokenValue();
        var refreshToken = new RefreshToken(
            user.Id,
            HashToken(refreshTokenValue),
            DateTime.UtcNow.AddDays(RefreshTokenExpiryDays));

        await _refreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken.Token, refreshTokenValue, accessToken.ExpiresAt);
    }

    private async Task RevokeAllActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await _refreshTokens.GetActiveByUserIdAsync(userId, cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }
    }

    private static UserResponse ToUserResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Role.ToString());

    private static string GenerateTokenValue() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
