using Microsoft.Data.SqlClient;

namespace TaskManagement.Infrastructure.Workers;

public interface ITransientFailureDetector
{
    bool IsTransient(Exception exception);
}

public sealed class TransientSqlFailureDetector : ITransientFailureDetector
{
    public bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && sqlException.IsTransient)
            {
                return true;
            }

            if (current is TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}