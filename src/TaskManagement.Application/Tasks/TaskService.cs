using FluentValidation;
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
    private readonly ITaskQueue _taskQueue;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskStatusRequest> _statusValidator;

    public TaskService(
        ITaskRepository tasks,
        IUnitOfWork unitOfWork,
        ICacheService cache,
        ITaskQueue taskQueue,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskStatusRequest> statusValidator)
    {
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _taskQueue = taskQueue;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
    }

    public async Task<Result<TaskResponse>> CreateAsync(
        Guid userId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaskResponse>(ToValidationError(validation));
        }

        var normalizedTitle = request.Title.Trim().ToUpperInvariant();
        var createdDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (await _tasks.ExistsForUserOnDateAsync(userId, normalizedTitle, createdDate, cancellationToken))
        {
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
            return TaskTitleConflict();
        }

        await _taskQueue.EnqueueAsync(TaskQueues.TaskProcessing, task.Id, cancellationToken);

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
            return Result.Failure<TaskResponse>(ToValidationError(validation));
        }

        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || task.UserId != userId)
        {
            return Result.Failure<TaskResponse>(Error.NotFound("Task not found.", "TaskNotFound"));
        }

        task.ChangeStatus(request.Status);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Result.Failure<TaskResponse>(
                Error.Conflict("The task was modified by another operation. Reload and try again.", "ConcurrencyConflict"));
        }

        await _cache.RemoveAsync(TaskCacheKeys.ForTask(userId, taskId), cancellationToken);

        return Result.Success(ToResponse(task));
    }

    private static Result<TaskResponse> TaskTitleConflict() =>
        Result.Failure<TaskResponse>(
            Error.Conflict("You already created a task with this title today.", "TaskTitleDuplicate"));

    private static TaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.Priority, task.CreatedAt);

    private static Error ToValidationError(FluentValidation.Results.ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Error.Validation(errors);
    }
}
