using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    private TaskItem()
    {
    }

    public TaskItem(string title, string? description, TaskPriority priority, Guid userId)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        Status = TaskItemStatus.Pending;
        Priority = priority;
        CreatedAt = DateTime.UtcNow;
        UserId = userId;
    }

    public void Update(string title, string? description, TaskItemStatus status, TaskPriority priority)
    {
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
    }

    public void ChangeStatus(TaskItemStatus status) => Status = status;
}
