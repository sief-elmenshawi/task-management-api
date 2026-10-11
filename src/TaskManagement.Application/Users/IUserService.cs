using TaskManagement.Application.Authentication.Dtos;
using TaskManagement.Application.Common;
using TaskManagement.Application.Users.Dtos;

namespace TaskManagement.Application.Users;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserAdminResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<UserAdminResponse>> CreateAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid currentUserId, Guid userId, CancellationToken cancellationToken = default);
}
