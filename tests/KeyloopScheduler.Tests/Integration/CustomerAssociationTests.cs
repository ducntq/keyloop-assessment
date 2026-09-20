using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Tests.Integration;

/// <summary>
/// Tier 2: Scenario A requirement 3 — a confirmed appointment must persist an
/// association to the customer alongside the vehicle, technician and service bay.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CustomerAssociationTests
{
    private readonly SchedulerApiFactory _factory;

    public CustomerAssociationTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateAppointmentRequest Request(Guid customerId, DateTime start) =>
        new()
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = customerId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = start
        };

    [Fact]
    public async Task Booking_persists_the_customer_association()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/appointments",
            Request(SeedData.CustomerAId, TestCalendar.At(70, 9, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        created!.CustomerId.Should().Be(SeedData.CustomerAId);

        var stored = await _factory.WithDbContextAsync(db =>
            db.Appointments.AsNoTracking().SingleAsync(a => a.Id == created.AppointmentId));

        stored.CustomerId.Should().Be(SeedData.CustomerAId);
    }

    [Fact]
    public async Task Unknown_customer_returns_404()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/appointments",
            Request(Guid.NewGuid(), TestCalendar.At(71, 9, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("Customer");
    }

    [Fact]
    public async Task Customer_from_another_dealership_returns_400()
    {
        var otherDealershipId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        await _factory.WithDbContextAsync(async db =>
        {
            db.Dealerships.Add(new Dealership(otherDealershipId, "Other Dealership"));
            db.Customers.Add(new Customer(
                otherCustomerId,
                otherDealershipId,
                "Casey Foreign",
                "casey.foreign@example.com"));

            await db.SaveChangesAsync();
        });

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/appointments",
            Request(otherCustomerId, TestCalendar.At(72, 9, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("dealership");
    }
}
