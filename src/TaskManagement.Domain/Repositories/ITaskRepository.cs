using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdForUserAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserOnDateAsync(
        Guid userId,
        string normalizedTitle,
        DateOnly createdDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetPendingOlderThanAsync(
        DateTime createdAtBefore,
        int limit,
        CancellationToken cancellationToken = default);
    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);
}
