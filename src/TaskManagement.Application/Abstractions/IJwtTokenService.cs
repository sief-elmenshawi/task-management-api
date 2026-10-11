using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Abstractions;

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);
