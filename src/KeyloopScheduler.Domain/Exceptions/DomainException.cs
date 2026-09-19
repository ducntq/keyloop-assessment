namespace KeyloopScheduler.Domain.Exceptions;

/// <summary>
/// Base type for every business-rule violation raised by the domain.
/// The API layer translates these into RFC 7807 problem responses.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
