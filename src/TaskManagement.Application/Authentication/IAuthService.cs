using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Common;

namespace TaskManagement.Application.Authentication;

public interface IAuthService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
