using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Entities;

/// <summary>
/// The customer who owns the vehicle being serviced. Scenario A requirement 3
/// requires a confirmed appointment to associate the customer alongside the
/// vehicle, technician and service bay, so a customer is a first-class catalogue
/// aggregate scoped to the dealership that serves them.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public bool IsActive { get; private set; }

    private Customer()
    {
        // Required by EF Core materialization.
    }

    public Customer(Guid id, Guid dealershipId, string fullName, string email)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Customer id must not be empty.");
        }

        if (dealershipId == Guid.Empty)
        {
            throw new DomainValidationException("Dealership id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainValidationException("Customer name must not be empty.");
        }

        if (!IsWellFormedEmail(email))
        {
            throw new DomainValidationException("Customer email must be a well-formed address.");
        }

        Id = id;
        DealershipId = dealershipId;
        FullName = fullName.Trim();
        Email = email.Trim();
        IsActive = true;
    }

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainValidationException("Customer name must not be empty.");
        }

        FullName = fullName.Trim();
    }

    public void ChangeEmail(string email)
    {
        if (!IsWellFormedEmail(email))
        {
            throw new DomainValidationException("Customer email must be a well-formed address.");
        }

        Email = email.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Deliberately lightweight, dependency-free shape check: a single separator
    /// with non-empty local part and a dotted domain. Full RFC 5322 validation is
    /// not this layer's responsibility.
    /// </summary>
    private static bool IsWellFormedEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');

        return at > 0
            && at == trimmed.LastIndexOf('@')
            && at < trimmed.Length - 1
            && trimmed.IndexOf('.', at) > at + 1;
    }
}
