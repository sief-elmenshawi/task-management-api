using System.Reflection;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Workers;

namespace TaskManagement.UnitTests.Infrastructure;

public class TransientSqlFailureDetectorTests
{
    private readonly TransientSqlFailureDetector _detector = new();

    [Fact]
    public void TimeoutException_IsTransient()
    {
        _detector.IsTransient(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void NonTransientSqlException_IsNotTransient()
    {
        _detector.IsTransient(CreateSqlException(2601)).Should().BeFalse();
    }

    [Fact]
    public void DbUpdateException_WrappingTransientTimeout_IsTransient()
    {
        var update = new DbUpdateException("save failed", new TimeoutException());

        _detector.IsTransient(update).Should().BeTrue();
    }

    [Fact]
    public void DbUpdateException_WrappingNonTransientSqlException_IsNotTransient()
    {
        var update = new DbUpdateException("save failed", CreateSqlException(2601));

        _detector.IsTransient(update).Should().BeFalse();
    }

    [Fact]
    public void DbUpdateException_WrappingNonTransientException_IsNotTransient()
    {
        var update = new DbUpdateException("save failed", new InvalidOperationException("permanent"));

        _detector.IsTransient(update).Should().BeFalse();
    }

    private static SqlException CreateSqlException(int infoNumber)
    {
        var collection = (SqlErrorCollection)Activator.CreateInstance(
            typeof(SqlErrorCollection),
            nonPublic: true)!;

        var errorCtor = typeof(SqlError).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(int), typeof(byte), typeof(byte),
                typeof(string), typeof(string), typeof(string),
                typeof(int), typeof(int), typeof(Exception)
            ],
            modifiers: null)!;

        var error = (SqlError)errorCtor.Invoke(
        [
            infoNumber, (byte)0, (byte)11, "server", "SQL Server error.", null, 1, infoNumber, null
        ])!;

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [error]);

        var sqlExceptionCtor = typeof(SqlException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid)],
            modifiers: null)!;

        return (SqlException)sqlExceptionCtor.Invoke(
        [
            $"SQL Server error {infoNumber}.", collection, null, Guid.Empty
        ])!;
    }
}