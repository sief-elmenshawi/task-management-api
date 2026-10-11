using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Dtos;

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateTime CreatedAt);
