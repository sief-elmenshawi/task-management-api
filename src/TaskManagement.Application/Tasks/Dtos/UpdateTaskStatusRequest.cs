using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Dtos;

public sealed record UpdateTaskStatusRequest(TaskItemStatus Status);
