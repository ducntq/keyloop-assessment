using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// The ownership-domain root: a dealership that owns service bays,
/// technicians and the service catalogue they operate.
/// </summary>
public sealed class Dealership
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    private Dealership()
    {
        // Required by EF Core materialization.
    }

    public Dealership(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Dealership id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Dealership name must not be empty.");
        }

        Id = id;
        Name = name.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Dealership name must not be empty.");
        }

        Name = name.Trim();
    }
}
