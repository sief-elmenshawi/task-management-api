using FluentValidation;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Tasks.Dtos;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Application.Tasks;

public sealed class TaskService : ITaskService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ITaskRepository _tasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ITaskProcessingQueue _taskQueue;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskStatusRequest> _statusValidator;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository tasks,
        IUnitOfWork unitOfWork,
        ICacheService cache,
        ITaskProcessingQueue taskQueue,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskStatusRequest> statusValidator,
        ILogger<TaskService> logger)
    {
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _taskQueue = taskQueue;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _logger = logger;
    }

    public async Task<Result<TaskResponse>> CreateAsync(
        Guid userId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaskResponse>(validation.ToError());
        }

        var normalizedTitle = request.Title.Trim().ToUpperInvariant();
        var createdDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (await _tasks.ExistsForUserOnDateAsync(userId, normalizedTitle, createdDate, cancellationToken))
        {
            _logger.LogInformation(
                "Rejected duplicate task title for user {UserId} on {CreatedDate}.",
                userId,
                createdDate);

            return TaskTitleConflict();
        }

        var task = new TaskItem(request.Title, request.Description, request.Priority, userId);

        await _tasks.AddAsync(task, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            _logger.LogInformation(
                "Rejected duplicate task title for user {UserId} on {CreatedDate} (database constraint).",
                userId,
                createdDate);

            return TaskTitleConflict();
        }

        await _taskQueue.EnqueueAsync(task.Id, cancellationToken);

        _logger.LogInformation("Created task {TaskId} for user {UserId}.", task.Id, userId);

        return Result.Success(ToResponse(task));
    }

    public async Task<Result<IReadOnlyList<TaskResponse>>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _tasks.GetByUserIdAsync(userId, cancellationToken);
        IReadOnlyList<TaskResponse> response = tasks.Select(ToResponse).ToList();

        return Result.Success(response);
    }

    public async Task<Result<TaskResponse>> GetByIdAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = TaskCacheKeys.ForTask(userId, taskId);

        var cached = await _cache.GetAsync<TaskResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || task.UserId != userId)
        {
            return Result.Failure<TaskResponse>(Error.NotFound("Task not found.", "TaskNotFound"));
        }

        var response = ToResponse(task);
        await _cache.SetAsync(cacheKey, response, CacheTtl, cancellationToken);

        return Result.Success(response);
    }

    public async Task<Result<TaskResponse>> UpdateStatusAsync(
        Guid userId,
        Guid taskId,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaskResponse>(validation.ToError());
        }

        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || task.UserId != userId)
        {
            _logger.LogDebug("Task {TaskId} was not found for user {UserId}.", taskId, userId);

            return Result.Failure<TaskResponse>(Error.NotFound("Task not found.", "TaskNotFound"));
        }

        task.ChangeStatus(request.Status);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict while updating task {TaskId}.", taskId);

            return Result.Failure<TaskResponse>(
                Error.Conflict("The task was modified by another operation. Reload and try again.", "ConcurrencyConflict"));
        }

        await _cache.RemoveAsync(TaskCacheKeys.ForTask(userId, taskId), cancellationToken);

        _logger.LogInformation(
            "Updated task {TaskId} status to {Status} for user {UserId}.",
            taskId,
            request.Status,
            userId);

        return Result.Success(ToResponse(task));
    }

    private static Result<TaskResponse> TaskTitleConflict() =>
        Result.Failure<TaskResponse>(
            Error.Conflict("You already created a task with this title today.", "TaskTitleDuplicate"));

    private static TaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.Priority, task.CreatedAt);
}
