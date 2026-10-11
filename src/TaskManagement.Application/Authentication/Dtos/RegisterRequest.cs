namespace TaskManagement.Application.Authentication.Dtos;

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string ConfirmPassword);
