using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Dtos;

public sealed record CreateTaskRequest(string Title, string? Description, TaskPriority Priority);
