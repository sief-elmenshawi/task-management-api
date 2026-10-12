using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Repositories;

namespace TaskManagement.UnitTests.Infrastructure;

public class TaskRepositoryPendingRecoveryTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateTime BaseTime =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPendingOlderThanAsync_ReturnsOnlyOldPendingTasks_OrderedByCreatedAt()
    {
        await using var context = CreateContext();
        var repository = new TaskRepository(context);

        var owner = new User("Owner", "owner@example.com", "hash", UserRole.User);
        context.Users.Add(owner);

        var olderPending = new TaskItem("Older pending", null, TaskPriority.Low, owner.Id);
        var oldPending = new TaskItem("Old pending", null, TaskPriority.Low, owner.Id);
        var recentPending = new TaskItem("Recent pending", null, TaskPriority.Low, owner.Id);
        var oldDone = new TaskItem("Old done", null, TaskPriority.Low, owner.Id);
        oldDone.ChangeStatus(TaskItemStatus.Done);

        context.Tasks.AddRange(olderPending, oldPending, recentPending, oldDone);

        SetCreatedAt(context, olderPending, BaseTime.AddMinutes(-10));
        SetCreatedAt(context, oldPending, BaseTime.AddMinutes(-5));
        SetCreatedAt(context, recentPending, BaseTime.AddMinutes(1));
        SetCreatedAt(context, oldDone, BaseTime.AddMinutes(-15));

        await context.SaveChangesAsync();

        var result = await repository.GetPendingOlderThanAsync(BaseTime, 100);

        result.Should().Equal(olderPending.Id, oldPending.Id);
    }

    [Fact]
    public async Task GetPendingOlderThanAsync_RespectsTheLimit()
    {
        await using var context = CreateContext();
        var repository = new TaskRepository(context);

        var owner = new User("Owner", "owner@example.com", "hash", UserRole.User);
        context.Users.Add(owner);

        var pending = Enumerable.Range(0, 150)
            .Select(index => new TaskItem($"Pending {index}", null, TaskPriority.Low, owner.Id))
            .ToList();
        context.Tasks.AddRange(pending);

        for (var index = 0; index < pending.Count; index++)
        {
            SetCreatedAt(context, pending[index], BaseTime.AddMinutes(-index));
        }

        await context.SaveChangesAsync();

        var result = await repository.GetPendingOlderThanAsync(BaseTime, 100);

        result.Should().HaveCount(100);
        result.Should().OnlyContain(id => pending.Any(task => task.Id == id));
    }

    private static void SetCreatedAt(AppDbContext context, TaskItem task, DateTime createdAt) =>
        context.Entry(task).Property(t => t.CreatedAt).CurrentValue = createdAt;
}