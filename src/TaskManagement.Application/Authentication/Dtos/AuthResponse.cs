namespace TaskManagement.Application.Authentication.Dtos;

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
