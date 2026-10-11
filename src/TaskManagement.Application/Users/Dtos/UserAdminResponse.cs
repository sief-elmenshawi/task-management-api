namespace TaskManagement.Application.Users.Dtos;

public sealed record UserAdminResponse(Guid Id, string Name, string Email, string Role, DateTime CreatedAt);
