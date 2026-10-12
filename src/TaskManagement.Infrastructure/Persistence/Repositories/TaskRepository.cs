using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<TaskItem?> GetByIdForUserAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsForUserOnDateAsync(
        Guid userId,
        string normalizedTitle,
        DateOnly createdDate,
        CancellationToken cancellationToken = default) =>
        _context.Tasks.AnyAsync(
            t => t.UserId == userId
                && t.NormalizedTitle == normalizedTitle
                && t.CreatedDate == createdDate,
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Status == TaskItemStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPendingOlderThanAsync(
        DateTime createdAtBefore,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .AsNoTracking()
            .Where(t => t.Status == TaskItemStatus.Pending && t.CreatedAt < createdAtBefore)
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default) =>
        await _context.Tasks.AddAsync(task, cancellationToken);
}
