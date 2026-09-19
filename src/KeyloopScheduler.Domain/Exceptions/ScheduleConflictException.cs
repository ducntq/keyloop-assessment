namespace KeyloopScheduler.Domain.Exceptions;

/// <summary>
/// Raised when a requested resource allocation cannot be satisfied because a
/// required physical or human resource is already booked (or a concurrency
/// collision was detected at the persistence layer).
/// Mapped to HTTP 409 Conflict.
/// </summary>
public sealed class ScheduleConflictException : DomainException
{
    public ScheduleConflictException(string message)
        : base(message)
    {
    }

    public ScheduleConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
