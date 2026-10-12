using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (IsRefreshTokenOnly(exception))
        {
            return await RetryRefreshTokenBatchAsync(exception, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException(
                "The record was modified by another operation since it was loaded.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new DuplicateKeyException(
                "A record with the same unique values already exists.",
                exception);
        }
    }

    private static bool IsRefreshTokenOnly(DbUpdateConcurrencyException exception) =>
        exception.Entries.Count > 0
        && exception.Entries.All(entry => entry.Entity is RefreshToken);

    private async Task<int> RetryRefreshTokenBatchAsync(
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var entry in exception.Entries)
            {
                await entry.ReloadAsync(cancellationToken);
            }

            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException retryException)
        {
            throw new ConcurrencyException(
                "The record was modified by another operation since it was loaded.",
                retryException);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && (sqlException.Number == 2601 || sqlException.Number == 2627);
}
