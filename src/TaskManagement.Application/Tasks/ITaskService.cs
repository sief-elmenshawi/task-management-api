using TaskManagement.Application.Common;
using TaskManagement.Application.Tasks.Dtos;

namespace TaskManagement.Application.Tasks;

public interface ITaskService
{
    Task<Result<TaskResponse>> CreateAsync(
        Guid userId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TaskResponse>>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<TaskResponse>> GetByIdAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<Result<TaskResponse>> UpdateStatusAsync(
        Guid userId,
        Guid taskId,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken = default);
}
