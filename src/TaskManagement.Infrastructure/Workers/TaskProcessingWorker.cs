using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Queues;

namespace TaskManagement.Infrastructure.Workers;

public sealed class TaskProcessingWorker : BackgroundService
{
    private const int MinDelayMilliseconds = 3000;
    private const int MaxDelayMilliseconds = 5000;

    private readonly ChannelTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskProcessingWorker> _logger;

    public TaskProcessingWorker(
        ChannelTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TaskProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task processing worker started.");

        var reader = _queue.Reader(TaskQueues.TaskProcessing);

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

        _logger.LogInformation("Task processing worker stopped.");
    }

    private async Task ProcessAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(MinDelayMilliseconds, MaxDelayMilliseconds + 1), cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
}
