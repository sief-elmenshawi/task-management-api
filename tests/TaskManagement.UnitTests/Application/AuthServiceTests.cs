using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Authentication.Validators;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.UnitTests.Application;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();

    private AuthService CreateSut() => CreateSut(new RefreshTokenOptions());

    private AuthService CreateSut(RefreshTokenOptions options) => new(
        _users,
        _refreshTokens,
        _unitOfWork,
        _passwordHasher,
        _jwtTokenService,
        new RegisterRequestValidator(),
        new LoginRequestValidator(),
        new OptionsWrapper<RefreshTokenOptions>(options),
        NullLogger<AuthService>.Instance);

    [Fact]
    public async Task RegisterAsync_ReturnsConflict_WhenEmailAlreadyExists()
    {
        _users.EmailExistsAsync("dup@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("Jane", "Dup@Example.com", "Password1", "Password1"));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("EmailAlreadyExists");
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ReturnsValidationError_AndSkipsRepository_WhenRequestInvalid()
    {
        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("", "not-an-email", "short", "mismatch"));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        await _users.DidNotReceive().EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmail_AndPersistsUser()
    {
        _users.EmailExistsAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("Password1").Returns("hashed");

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("  Jane  ", "  Jane@Example.com ", "Password1", "Password1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("jane@example.com");
        result.Value.Name.Should().Be("Jane");
        result.Value.Role.Should().Be("User");
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "jane@example.com" && u.PasswordHash == "hashed"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenPasswordInvalid()
    {
        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);
        _users.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong", "hashed").Returns(false);

        var result = await CreateSut().LoginAsync(new LoginRequest("jane@example.com", "wrong"));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task LoginAsync_IssuesTokens_WhenCredentialsValid()
    {
        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);
        _users.GetByEmailAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Password1", "hashed").Returns(true);
        _jwtTokenService.CreateAccessToken(user)
            .Returns(new AccessToken("access-token", DateTime.UtcNow.AddMinutes(15)));

        var result = await CreateSut().LoginAsync(new LoginRequest("Jane@Example.com", "Password1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RotatesToken_WhenStoredTokenIsActive()
    {
        var userId = Guid.NewGuid();
        var stored = new RefreshToken(userId, "old-hash", DateTime.UtcNow.AddDays(1));
        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);

        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _jwtTokenService.CreateAccessToken(user)
            .Returns(new AccessToken("new-access", DateTime.UtcNow.AddMinutes(15)));

        var result = await CreateSut().RefreshAsync(new RefreshTokenRequest("raw-refresh-token"));

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access");
        stored.IsRevoked.Should().BeTrue();
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RevokesAllSessions_WhenRotatedTokenPresentedAfterGraceWindow()
    {
        var userId = Guid.NewGuid();
        var reused = new RefreshToken(userId, "reused-hash", DateTime.UtcNow.AddDays(1));
        reused.Revoke("replacement-hash");

        var stillActive = new RefreshToken(userId, "active-hash", DateTime.UtcNow.AddDays(1));

        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(reused);
        _refreshTokens.GetActiveByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { stillActive });

        var result = await CreateSut(new RefreshTokenOptions { ReuseGraceSeconds = -1 })
            .RefreshAsync(new RefreshTokenRequest("raw-refresh-token"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RefreshTokenReuseDetected");
        stillActive.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReturnsInvalidToken_WhenRecentlyRotatedTokenPresented()
    {
        var userId = Guid.NewGuid();
        var rotated = new RefreshToken(userId, "rotated-hash", DateTime.UtcNow.AddDays(1));
        rotated.Revoke("replacement-hash");

        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(rotated);

        var result = await CreateSut().RefreshAsync(new RefreshTokenRequest("raw-refresh-token"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvalidRefreshToken");
        await _refreshTokens.DidNotReceive()
            .GetActiveByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReturnsInvalidToken_WhenRotationConflictsWithConcurrentRequest()
    {
        var userId = Guid.NewGuid();
        var stored = new RefreshToken(userId, "old-hash", DateTime.UtcNow.AddDays(1));
        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);

        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _jwtTokenService.CreateAccessToken(user)
            .Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("conflict", new InvalidOperationException()));

        var result = await CreateSut().RefreshAsync(new RefreshTokenRequest("raw-refresh-token"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvalidRefreshToken");
    }

    [Fact]
    public async Task RefreshAsync_ReturnsUnauthorized_WhenTokenUnknown()
    {
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await CreateSut().RefreshAsync(new RefreshTokenRequest("unknown"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvalidRefreshToken");
    }

    [Fact]
    public async Task RefreshAsync_ReturnsUnauthorized_WhenUserNoLongerExists()
    {
        var stored = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(1));
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(stored.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateSut().RefreshAsync(new RefreshTokenRequest("raw-refresh-token"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvalidRefreshToken");
        await _refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken_WhenActive()
    {
        var stored = new RefreshToken(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(1));
        _refreshTokens.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(stored);

        var result = await CreateSut().LogoutAsync(new LogoutRequest("raw"));

        result.IsSuccess.Should().BeTrue();
        stored.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
