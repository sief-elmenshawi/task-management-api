using FluentAssertions;
using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Authentication.Validators;
using TaskManagement.Application.Common;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.UnitTests.Application;

public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private UserService CreateSut() => new(
        _users,
        _refreshTokens,
        _unitOfWork,
        _passwordHasher,
        new RegisterRequestValidator());

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_WhenDeletingSelf()
    {
        var userId = Guid.NewGuid();

        var result = await CreateSut().DeleteAsync(userId, userId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("CannotDeleteSelf");
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenUserMissing()
    {
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateSut().DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UserNotFound");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesUser_AndRevokesActiveTokens()
    {
        var currentUser = Guid.NewGuid();
        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);
        var activeToken = new RefreshToken(user.Id, "hash", DateTime.UtcNow.AddDays(1));

        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _refreshTokens.GetActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { activeToken });

        var result = await CreateSut().DeleteAsync(currentUser, user.Id);

        result.IsSuccess.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        activeToken.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenEmailExists()
    {
        _users.EmailExistsAsync("jane@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().CreateAsync(
            new RegisterRequest("Jane", "jane@example.com", "Password1", "Password1"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EmailAlreadyExists");
    }

    [Fact]
    public async Task CreateAsync_CreatesRegularUser()
    {
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");

        var result = await CreateSut().CreateAsync(
            new RegisterRequest("Jane", "jane@example.com", "Password1", "Password1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("User");
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
