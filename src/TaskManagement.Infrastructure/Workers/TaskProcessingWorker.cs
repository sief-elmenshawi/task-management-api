using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Queues;

namespace TaskManagement.Infrastructure.Workers;

public class TaskProcessingWorker : BackgroundService
{
    private const int MinDelayMilliseconds = 3000;
    private const int MaxDelayMilliseconds = 5000;

    private readonly ChannelTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkerOptions> _options;
    private readonly ILogger<TaskProcessingWorker> _logger;

    public TaskProcessingWorker(
        ChannelTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<TaskProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Max(1, _options.Value.Concurrency);

        _logger.LogInformation(
            "Task processing worker started with {Concurrency} consumer(s).",
            concurrency);

        var recovery = RecoverPendingTasksAsync(stoppingToken);

        var consumers = new Task[concurrency];
        for (var index = 0; index < concurrency; index++)
        {
            consumers[index] = ConsumeAsync(stoppingToken);
        }

        try
        {
            await Task.WhenAll(consumers);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during host shutdown.
        }

        await recovery;

        _logger.LogInformation("Task processing worker stopped.");
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;

        try
        {
            await foreach (var taskId in reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(taskId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to process task {TaskId}.", taskId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during host shutdown.
        }
    }

    internal async Task RecoverPendingTasksAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

            var pending = await repository.GetPendingAsync(cancellationToken);

            foreach (var taskId in pending)
            {
                await _queue.EnqueueAsync(taskId, cancellationToken);
            }

            _logger.LogInformation("Recovered {Count} pending task(s) after startup.", pending.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Pending task recovery was cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to recover pending tasks after startup.");
        }
    }

    internal async Task ProcessAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await DelayAsync(cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var task = await repository.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            _logger.LogDebug("Task {TaskId} no longer exists. Skipping.", taskId);
            return;
        }

        if (task.Status != TaskItemStatus.Pending)
        {
            _logger.LogDebug(
                "Task {TaskId} is {Status}; skipping automatic transition.",
                taskId,
                task.Status);

            return;
        }

        task.ChangeStatus(TaskItemStatus.InProgress);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Entry(task).ReloadAsync(cancellationToken);

            _logger.LogInformation(
                "Task {TaskId} was modified concurrently; it is now {Status}. Skipping.",
                taskId,
                task.Status);

            return;
        }

        await cache.RemoveAsync(TaskCacheKeys.ForTask(task.UserId, task.Id), cancellationToken);

        _logger.LogInformation("Task {TaskId} moved from Pending to InProgress.", taskId);
    }

    protected virtual Task DelayAsync(CancellationToken cancellationToken) =>
        Task.Delay(Random.Shared.Next(MinDelayMilliseconds, MaxDelayMilliseconds + 1), cancellationToken);
}
