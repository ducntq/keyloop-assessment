namespace KeyloopScheduler.Domain.Exceptions;

/// <summary>
/// Raised when a referenced aggregate (dealership, service bay, technician,
/// service type or appointment) does not exist.
/// Mapped to HTTP 404 Not Found.
/// </summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string message)
        : base(message)
    {
    }

    public static EntityNotFoundException For(string entityName, object key) =>
        new($"{entityName} with identifier '{key}' was not found.");
}
