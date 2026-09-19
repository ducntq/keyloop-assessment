using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// A physical service bay (lift, rack, EV bay, ...) that can host one
/// appointment at a time.
/// </summary>
public sealed class ServiceBay
{
    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    private ServiceBay()
    {
        // Required by EF Core materialization.
    }

    public ServiceBay(Guid id, Guid dealershipId, string name)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Service bay id must not be empty.");
        }

        if (dealershipId == Guid.Empty)
        {
            throw new DomainValidationException("Dealership id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Service bay name must not be empty.");
        }

        Id = id;
        DealershipId = dealershipId;
        Name = name.Trim();
        IsActive = true;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
