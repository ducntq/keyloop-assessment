using FluentAssertions;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: customer identity and lifecycle guards.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CustomerTests
{
    private static readonly Guid Id = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DealershipId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public void Constructor_creates_an_active_customer()
    {
        var customer = new Customer(Id, DealershipId, "Alice Nguyen", "alice.nguyen@example.com");

        customer.Id.Should().Be(Id);
        customer.DealershipId.Should().Be(DealershipId);
        customer.FullName.Should().Be("Alice Nguyen");
        customer.Email.Should().Be("alice.nguyen@example.com");
        customer.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_rejects_an_empty_id()
    {
        var act = () => new Customer(Guid.Empty, DealershipId, "Alice Nguyen", "alice.nguyen@example.com");

        act.Should().Throw<DomainValidationException>().WithMessage("*Customer id*");
    }

    [Fact]
    public void Constructor_rejects_an_empty_dealership_id()
    {
        var act = () => new Customer(Id, Guid.Empty, "Alice Nguyen", "alice.nguyen@example.com");

        act.Should().Throw<DomainValidationException>().WithMessage("*Dealership id*");
    }

    [Fact]
    public void Constructor_rejects_a_blank_name()
    {
        var act = () => new Customer(Id, DealershipId, "  ", "alice.nguyen@example.com");

        act.Should().Throw<DomainValidationException>().WithMessage("*name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@example.com")]
    public void Constructor_rejects_a_malformed_email(string email)
    {
        var act = () => new Customer(Id, DealershipId, "Alice Nguyen", email);

        act.Should().Throw<DomainValidationException>().WithMessage("*email*");
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_the_active_state()
    {
        var customer = new Customer(Id, DealershipId, "Alice Nguyen", "alice.nguyen@example.com");

        customer.Deactivate();
        customer.IsActive.Should().BeFalse();

        customer.Activate();
        customer.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rename_rejects_a_blank_name()
    {
        var customer = new Customer(Id, DealershipId, "Alice Nguyen", "alice.nguyen@example.com");

        var act = () => customer.Rename("  ");

        act.Should().Throw<DomainValidationException>().WithMessage("*name*");
    }
}
