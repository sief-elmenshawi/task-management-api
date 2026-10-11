using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Repositories;
using TaskManagement.Infrastructure.Queues;
using TaskManagement.Infrastructure.Workers;

namespace TaskManagement.UnitTests.Infrastructure;

public class TaskProcessingWorkerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ChannelTaskQueue _queue = new();

    private TestWorker CreateWorker(out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_tasks);
        services.AddSingleton(_unitOfWork);
        services.AddSingleton(_cache);
        provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new TestWorker(_queue, scopeFactory, NullLogger<TaskProcessingWorker>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_MovesPendingTaskToInProgress_AndEvictsCache()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await worker.ProcessAsync(task.Id, CancellationToken.None);

        task.Status.Should().Be(TaskItemStatus.InProgress);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            TaskCacheKeys.ForTask(task.UserId, task.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_Skips_WhenTaskIsNotPending()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        task.ChangeStatus(TaskItemStatus.Done);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await worker.ProcessAsync(task.Id, CancellationToken.None);

        task.Status.Should().Be(TaskItemStatus.Done);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_Skips_WhenTaskNoLongerExists()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);

        await worker.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverPendingTasksAsync_EnqueuesAllPendingTasks()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        var pending = new List<TaskItem>
        {
            new("First", null, TaskPriority.Low, Guid.NewGuid()),
            new("Second", null, TaskPriority.High, Guid.NewGuid())
        };
        _tasks.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(pending);

        await worker.RecoverPendingTasksAsync(CancellationToken.None);

        var reader = _queue.Reader(TaskQueues.TaskProcessing);
        var recovered = new List<Guid>();
        while (reader.TryRead(out var id))
        {
            recovered.Add(id);
        }

        recovered.Should().BeEquivalentTo(new[] { pending[0].Id, pending[1].Id });
    }

    private sealed class TestWorker : TaskProcessingWorker
    {
        public TestWorker(
            ChannelTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Logging.ILogger<TaskProcessingWorker> logger)
            : base(queue, scopeFactory, logger)
        {
        }

        protected override Task DelayAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
