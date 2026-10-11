namespace TaskManagement.Application.Authentication.Dtos;

public sealed record UserResponse(Guid Id, string Name, string Email, string Role);
