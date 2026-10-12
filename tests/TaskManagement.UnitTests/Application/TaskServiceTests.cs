using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Common;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.Dtos;
using TaskManagement.Application.Tasks.Validators;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.UnitTests.Application;

public class TaskServiceTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ITaskProcessingQueue _taskQueue = Substitute.For<ITaskProcessingQueue>();

    private TaskService CreateSut() => new(
        _tasks,
        _unitOfWork,
        _cache,
        _taskQueue,
        new CreateTaskRequestValidator(),
        new UpdateTaskStatusRequestValidator(),
        NullLogger<TaskService>.Instance);

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenTitleExistsForUserToday()
    {
        _tasks.ExistsForUserOnDateAsync(
                Arg.Any<Guid>(), "MY TASK", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateSut().CreateAsync(
            Guid.NewGuid(), new CreateTaskRequest("My Task", null, TaskPriority.Medium));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TaskTitleDuplicate");
        await _taskQueue.DidNotReceive().EnqueueAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_SavesThenEnqueues_WhenValid()
    {
        var userId = Guid.NewGuid();
        _tasks.ExistsForUserOnDateAsync(
                userId, "MY TASK", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateSut().CreateAsync(
            userId, new CreateTaskRequest("My Task", "desc", TaskPriority.High));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TaskItemStatus.Pending);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _taskQueue.Received(1).EnqueueAsync(
            result.Value.Id, Arg.Any<CancellationToken>());

        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _taskQueue.EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_AndDoesNotEnqueue_WhenUniqueConstraintTrips()
    {
        var userId = Guid.NewGuid();
        _tasks.ExistsForUserOnDateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new DuplicateKeyException("dup", new Exception("constraint"))));

        var result = await CreateSut().CreateAsync(
            userId, new CreateTaskRequest("My Task", null, TaskPriority.Low));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TaskTitleDuplicate");
        await _taskQueue.DidNotReceive().EnqueueAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCachedValue_WithoutHittingRepository()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var cached = new TaskResponse(taskId, "cached", null, TaskItemStatus.Pending, TaskPriority.Low, DateTime.UtcNow);
        _cache.GetAsync<TaskResponse>(TaskCacheKeys.ForTask(userId, taskId), Arg.Any<CancellationToken>())
            .Returns(cached);

        var result = await CreateSut().GetByIdAsync(userId, taskId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cached);
        await _tasks.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_PopulatesCache_OnMiss()
    {
        var userId = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, userId);
        _cache.GetAsync<TaskResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TaskResponse?)null);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await CreateSut().GetByIdAsync(userId, task.Id);

        result.IsSuccess.Should().BeTrue();
        await _cache.Received(1).SetAsync(
            TaskCacheKeys.ForTask(userId, task.Id),
            Arg.Any<TaskResponse>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenTaskBelongsToAnotherUser()
    {
        var currentUser = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        _cache.GetAsync<TaskResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TaskResponse?)null);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await CreateSut().GetByIdAsync(currentUser, task.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TaskNotFound");
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<TaskResponse>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusAsync_EvictsCache_WhenStatusChanged()
    {
        var userId = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, userId);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await CreateSut().UpdateStatusAsync(
            userId, task.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TaskItemStatus.Done);
        await _cache.Received(1).RemoveAsync(TaskCacheKeys.ForTask(userId, task.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsConflict_OnConcurrencyException()
    {
        var userId = Guid.NewGuid();
        var task = new TaskItem("Task", null, TaskPriority.Low, userId);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new ConcurrencyException("conflict", new Exception("rowversion"))));

        var result = await CreateSut().UpdateStatusAsync(
            userId, task.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ConcurrencyConflict");
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNotFound_WhenOwnedByAnotherUser()
    {
        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await CreateSut().UpdateStatusAsync(
            Guid.NewGuid(), task.Id, new UpdateTaskStatusRequest(TaskItemStatus.Done));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TaskNotFound");
    }
}
