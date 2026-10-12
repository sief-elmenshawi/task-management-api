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

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
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

    public async Task<IReadOnlyList<TaskItem>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await _context.Tasks
            .Where(t => t.Status == TaskItemStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default) =>
        await _context.Tasks.AddAsync(task, cancellationToken);

    public void Update(TaskItem task) => _context.Tasks.Update(task);
}
