using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Extensions;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.Dtos;

namespace TaskManagement.Api.Controllers;

/// <summary>
/// Task endpoints for the authenticated user. Every task is scoped to the caller.
/// </summary>
[ApiController]
[Route("api/tasks")]
[Authorize]
[Tags("Tasks")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>
    /// Creates a task for the current user. A user cannot create two tasks with the same title on the same day.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _taskService.CreateAsync(userId, request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToProblemDetails();
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Lists the current user's tasks, ordered by priority (high to low) then by creation date (newest first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _taskService.GetForUserAsync(userId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails();
    }

    /// <summary>
    /// Returns a single task owned by the current user. Tasks owned by other users are reported as not found.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _taskService.GetByIdAsync(userId, id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails();
    }

    /// <summary>
    /// Updates the status of a task owned by the current user.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _taskService.UpdateStatusAsync(userId, id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails();
    }
}
