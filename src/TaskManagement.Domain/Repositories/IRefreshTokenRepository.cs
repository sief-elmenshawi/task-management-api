using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<int> RevokeAllActiveAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
