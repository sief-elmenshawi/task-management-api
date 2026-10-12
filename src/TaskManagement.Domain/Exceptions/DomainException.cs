namespace TaskManagement.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class DuplicateKeyException : DomainException
{
    public DuplicateKeyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
