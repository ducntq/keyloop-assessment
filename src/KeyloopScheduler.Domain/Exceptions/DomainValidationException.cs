namespace KeyloopScheduler.Domain.Exceptions;

/// <summary>
/// Raised when a request violates an input or domain invariant
/// (non-UTC timestamps, malformed VIN, business-hours breach, non-quantized start, ...).
/// Mapped to HTTP 400 Bad Request.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
