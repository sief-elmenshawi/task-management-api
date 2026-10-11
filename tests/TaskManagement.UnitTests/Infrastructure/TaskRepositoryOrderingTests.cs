using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Repositories;

namespace TaskManagement.UnitTests.Infrastructure;

public class TaskRepositoryOrderingTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyOwnTasks_OrderedByPriorityThenCreatedAtDescending()
    {
        await using var context = CreateContext();
        var repository = new TaskRepository(context);

        var owner = new User("Owner", "owner@example.com", "hash", UserRole.User);
        var other = new User("Other", "other@example.com", "hash", UserRole.User);
        context.Users.AddRange(owner, other);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var highOlder = new TaskItem("High older", null, TaskPriority.High, owner.Id);
        var highNewer = new TaskItem("High newer", null, TaskPriority.High, owner.Id);
        var medium = new TaskItem("Medium", null, TaskPriority.Medium, owner.Id);
        var low = new TaskItem("Low", null, TaskPriority.Low, owner.Id);
        var otherUsersTask = new TaskItem("Other high", null, TaskPriority.High, other.Id);

        context.Tasks.AddRange(highOlder, highNewer, medium, low, otherUsersTask);

        SetCreatedAt(context, highOlder, baseTime);
        SetCreatedAt(context, highNewer, baseTime.AddMinutes(5));
        SetCreatedAt(context, medium, baseTime.AddMinutes(1));
        SetCreatedAt(context, low, baseTime.AddMinutes(10));
        SetCreatedAt(context, otherUsersTask, baseTime.AddMinutes(30));

        await context.SaveChangesAsync();

        var result = await repository.GetByUserIdAsync(owner.Id);

        result.Select(t => t.Id).Should().Equal(highNewer.Id, highOlder.Id, medium.Id, low.Id);
        result.Should().OnlyContain(t => t.UserId == owner.Id);
        result.Should().NotContain(t => t.Id == otherUsersTask.Id);
    }

    private static void SetCreatedAt(AppDbContext context, TaskItem task, DateTime createdAt) =>
        context.Entry(task).Property(t => t.CreatedAt).CurrentValue = createdAt;
}
