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

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
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
