using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Queues;
using TaskManagement.Infrastructure.Workers;

namespace TaskManagement.UnitTests.Infrastructure;

public class TaskProcessingWorkerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ITransientFailureDetector _detector = Substitute.For<ITransientFailureDetector>();
    private readonly ChannelTaskQueue _queue = new();

    private TestWorker CreateWorker(out ServiceProvider provider, AppDbContext? context = null, int concurrency = 1)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_tasks);
        services.AddSingleton(_unitOfWork);
        services.AddSingleton(_cache);
        if (context is not null)
        {
            services.AddSingleton(context);
        }

        provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var options = Options.Create(new WorkerOptions { Concurrency = concurrency });
        return new TestWorker(_queue, scopeFactory, options, _detector, NullLogger<TaskProcessingWorker>.Instance);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
    public async Task ProcessAsync_ReloadsAndSkips_WhenSaveFailsWithConcurrencyException()
    {
        await using var context = CreateContext();

        var owner = new User("Owner", "owner@example.com", "hash", UserRole.User);
        var task = new TaskItem("Task", null, TaskPriority.Low, owner.Id);
        context.Users.Add(owner);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var worker = CreateWorker(out var provider, context);
        using var _ = provider;

        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(
                new ConcurrencyException("conflict", new Exception("rowversion"))));

        var act = () => worker.ProcessAsync(task.Id, CancellationToken.None);

        await act.Should().NotThrowAsync();
        task.Status.Should().Be(TaskItemStatus.Pending);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverPendingTasksAsync_EnqueuesAllPendingTasks()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        var pending = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _tasks.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(pending);

        await worker.RecoverPendingTasksAsync(CancellationToken.None);

        var reader = _queue.Reader;
        var recovered = new List<Guid>();
        while (reader.TryRead(out var id))
        {
            recovered.Add(id);
        }

        recovered.Should().BeEquivalentTo(pending);
    }

    [Fact]
    public async Task ExecuteAsync_RunsStartupRecovery()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        _tasks.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        await _tasks.Received().GetPendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWithRetryAsync_Retries_WhenTransientFailureOccurs()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        _detector.IsTransient(Arg.Any<Exception>()).Returns(true);

        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        var attempts = 0;
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new TimeoutException("transient");
                }

                return task;
            });

        await worker.ProcessWithRetryAsync(task.Id, CancellationToken.None);

        attempts.Should().Be(2);
        task.Status.Should().Be(TaskItemStatus.InProgress);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWithRetryAsync_DoesNotRetry_WhenFailureIsNotTransient()
    {
        var worker = CreateWorker(out var provider);
        using var _ = provider;

        _detector.IsTransient(Arg.Any<Exception>()).Returns(false);

        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        var attempts = 0;
        Func<CallInfo, TaskItem?> getById = _ =>
        {
            attempts++;
            throw new InvalidOperationException("permanent");
        };
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(getById);

        var act = async () => await worker.ProcessWithRetryAsync(task.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1);
        task.Status.Should().Be(TaskItemStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesAllQueuedTasks_WithMultipleConsumers()
    {
        var worker = CreateWorker(out var provider, concurrency: 3);
        using var _ = provider;

        _tasks.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var tasks = Enumerable.Range(0, 3)
            .Select(index => new TaskItem($"Task {index}", null, TaskPriority.Low, Guid.NewGuid()))
            .ToList();

        foreach (var task in tasks)
        {
            _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        }

        await worker.StartAsync(CancellationToken.None);

        foreach (var task in tasks)
        {
            await _queue.EnqueueAsync(task.Id);
        }

        await WaitUntilAsync(
            () => tasks.All(task => task.Status == TaskItemStatus.InProgress),
            TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);

        tasks.Should().OnlyContain(task => task.Status == TaskItemStatus.InProgress);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private sealed class TestWorker : TaskProcessingWorker
    {
        public TestWorker(
            ChannelTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            IOptions<WorkerOptions> options,
            ITransientFailureDetector transientFailureDetector,
            Microsoft.Extensions.Logging.ILogger<TaskProcessingWorker> logger)
            : base(queue, scopeFactory, options, transientFailureDetector, logger)
        {
        }

        protected override Task DelayAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
