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
    private const int RecoveryBatchSize = 100;

    private readonly ChannelTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkerOptions> _options;
    private readonly ITransientFailureDetector _transientFailureDetector;
    private readonly ILogger<TaskProcessingWorker> _logger;

    public TaskProcessingWorker(
        ChannelTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ITransientFailureDetector transientFailureDetector,
        ILogger<TaskProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options;
        _transientFailureDetector = transientFailureDetector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Max(1, _options.Value.Concurrency);

        _logger.LogInformation(
            "Task processing worker started with {Concurrency} consumer(s).",
            concurrency);

        var recovery = RecoverPendingTasksAsync(stoppingToken);
        var periodicRecovery = RunPeriodicRecoveryAsync(stoppingToken);

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
        await periodicRecovery;

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
                    await ProcessWithRetryAsync(taskId, stoppingToken);
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

    internal async Task ProcessWithRetryAsync(Guid taskId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await ProcessAsync(taskId, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (_transientFailureDetector.IsTransient(exception) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Transient failure processing task {TaskId} on attempt {Attempt} of {MaxAttempts}; retrying after a short backoff.",
                    taskId,
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
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

            _logger.LogInformation("Recovered {Count} pending task(s).", pending.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Pending task recovery was cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to recover pending tasks.");
        }
    }

    private async Task RunPeriodicRecoveryAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Value.RecoveryIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecoverOlderPendingOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during host shutdown.
        }
    }

    internal async Task RecoverOlderPendingOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

            var minAge = TimeSpan.FromSeconds(Math.Max(0, _options.Value.RecoveryMinAgeSeconds));
            var cutoff = DateTime.UtcNow - minAge;
            var pending = await repository.GetPendingOlderThanAsync(cutoff, RecoveryBatchSize, cancellationToken);

            foreach (var taskId in pending.Take(RecoveryBatchSize))
            {
                await _queue.EnqueueAsync(taskId, cancellationToken);
            }

            if (pending.Count > 0)
            {
                _logger.LogInformation(
                    "Periodic recovery re-enqueued {Count} pending task(s) older than {MinAge} seconds.",
                    pending.Count,
                    minAge.TotalSeconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Periodic recovery was cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to run periodic pending-task recovery.");
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
