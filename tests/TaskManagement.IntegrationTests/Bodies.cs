namespace TaskManagement.IntegrationTests;

internal sealed record RegisterBody(string Name, string Email, string Password, string ConfirmPassword);

internal sealed record LoginBody(string Email, string Password);

internal sealed record RefreshBody(string RefreshToken);

internal sealed record CreateTaskBody(string Title, string? Description, string Priority);

internal sealed record UpdateTaskStatusBody(string Status);

internal sealed record AuthResponseBody(string AccessToken, string RefreshToken, DateTime ExpiresAt);
