using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Authentication.Validators;
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
