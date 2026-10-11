using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication;
using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Authentication.Validators;
using TaskManagement.Application.Common;
using TaskManagement.Application.Users;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Repositories;

namespace TaskManagement.UnitTests.Infrastructure;

public class SoftDeleteQueryFilterTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task RefreshToken_IsHidden_AfterItsUserIsSoftDeleted()
    {
        await using var context = CreateContext();
        var repository = new RefreshTokenRepository(context);

        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);
        var token = new RefreshToken(user.Id, "token-hash", DateTime.UtcNow.AddDays(1));
        context.Users.Add(user);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        (await repository.GetByTokenHashAsync("token-hash")).Should().NotBeNull();

        user.MarkAsDeleted();
        await context.SaveChangesAsync();

        (await repository.GetByTokenHashAsync("token-hash")).Should().BeNull();
    }

    [Fact]
    public async Task PendingTask_IsHidden_AfterItsUserIsSoftDeleted()
    {
        await using var context = CreateContext();
        var repository = new TaskRepository(context);

        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);
        var task = new TaskItem("Task", null, TaskPriority.Low, user.Id);
        context.Users.Add(user);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        (await repository.GetPendingAsync()).Should().HaveCount(1);

        user.MarkAsDeleted();
        await context.SaveChangesAsync();

        (await repository.GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_AfterTheUserIsSoftDeleted()
    {
        await using var context = CreateContext();

        var user = new User("Jane", "jane@example.com", "hashed", UserRole.User);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify("Password1", "hashed").Returns(true);

        var jwtTokenService = Substitute.For<IJwtTokenService>();
        jwtTokenService.CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access", DateTime.UtcNow.AddMinutes(15)));

        var service = new AuthService(
            new UserRepository(context),
            new RefreshTokenRepository(context),
            new UnitOfWork(context),
            passwordHasher,
            jwtTokenService,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            NullLogger<AuthService>.Instance);

        var beforeDelete = await service.LoginAsync(new LoginRequest("jane@example.com", "Password1"));
        beforeDelete.IsSuccess.Should().BeTrue();

        user.MarkAsDeleted();
        await context.SaveChangesAsync();

        var afterDelete = await service.LoginAsync(new LoginRequest("jane@example.com", "Password1"));

        afterDelete.IsFailure.Should().BeTrue();
        afterDelete.Error.Type.Should().Be(ErrorType.Unauthorized);
        afterDelete.Error.Code.Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task DeleteAsync_RevokesAllActiveTokens_AndSoftDeletesTheUser()
    {
        await using var context = CreateContext();

        var user = new User("Jane", "jane@example.com", "hash", UserRole.User);
        var firstActive = new RefreshToken(user.Id, "hash-1", DateTime.UtcNow.AddDays(1));
        var secondActive = new RefreshToken(user.Id, "hash-2", DateTime.UtcNow.AddDays(1));
        var alreadyRevoked = new RefreshToken(user.Id, "hash-3", DateTime.UtcNow.AddDays(1));
        alreadyRevoked.Revoke();

        context.Users.Add(user);
        context.RefreshTokens.AddRange(firstActive, secondActive, alreadyRevoked);
        await context.SaveChangesAsync();

        var service = new UserService(
            new UserRepository(context),
            new RefreshTokenRepository(context),
            new UnitOfWork(context),
            Substitute.For<IPasswordHasher>(),
            new RegisterRequestValidator());

        var result = await service.DeleteAsync(Guid.NewGuid(), user.Id);

        result.IsSuccess.Should().BeTrue();

        var storedUser = await context.Users
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == user.Id);
        storedUser.IsDeleted.Should().BeTrue();

        var tokens = await context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == user.Id)
            .ToListAsync();

        tokens.Should().HaveCount(3);
        tokens.Should().OnlyContain(t => t.IsRevoked);
    }
}
