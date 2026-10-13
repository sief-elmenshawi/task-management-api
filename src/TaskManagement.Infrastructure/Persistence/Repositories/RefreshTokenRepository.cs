using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Repositories;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<int> RevokeAllActiveAsync(
        Guid userId,
        DateTime revokedAt,
        CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RevokedAt, revokedAt),
                cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
}
