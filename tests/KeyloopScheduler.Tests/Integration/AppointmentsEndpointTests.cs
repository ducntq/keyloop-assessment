using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Tests.Integration;

/// <summary>
/// Tier 2: booking + cancellation contract tests against a real PostgreSQL instance.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AppointmentsEndpointTests
{
    private readonly SchedulerApiFactory _factory;

    public AppointmentsEndpointTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateAppointmentRequest Request(DateTime start, Guid? bayId = null, Guid? technicianId = null) =>
        new()
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = bayId,
            TechnicianId = technicianId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = start
        };

    [Fact]
    public async Task Post_returns_201_with_full_relational_ids_and_a_location_header()
    {
        using var client = _factory.CreateClient();
        var start = TestCalendar.At(10, 9, 0);

        var response = await client.PostAsJsonAsync("/api/appointments", Request(start));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        created.Should().NotBeNull();
        created!.AppointmentId.Should().NotBeEmpty();
        created.DealershipId.Should().Be(SeedData.DealershipId);
        created.CustomerId.Should().Be(SeedData.CustomerAId);
        created.ServiceBayId.Should().NotBeEmpty();
        created.TechnicianId.Should().NotBeEmpty();
        created.ServiceTypeId.Should().Be(SeedData.OilAndInspectionServiceTypeId);
        created.VehicleIdentification.Should().Be("1HGBH41JXMN109186");
        created.Status.Should().Be(nameof(AppointmentStatus.Scheduled));
        created.EndTimeUtc.Should().Be(start.AddMinutes(30));

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(created.AppointmentId.ToString());
    }

    [Fact]
    public async Task Post_auto_assigns_a_certified_technician()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments", Request(TestCalendar.At(11, 9, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AppointmentResponse>();

        // Oil & Inspection requires GENERAL certification: Tech A, B and C hold it; Tech D does not.
        new[] { SeedData.TechAId, SeedData.TechBId, SeedData.TechCId }
            .Should().Contain(created!.TechnicianId);
    }

    [Fact]
    public async Task Get_by_id_returns_the_booked_appointment()
    {
        using var client = _factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/appointments", Request(TestCalendar.At(12, 9, 0))))
            .Content.ReadFromJsonAsync<AppointmentResponse>();

        var response = await client.GetAsync($"/api/appointments/{created!.AppointmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        fetched.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_an_unknown_appointment()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/appointments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_an_appointment_releases_its_resources()
    {
        using var client = _factory.CreateClient();
        var start = TestCalendar.At(13, 9, 0);
        var request = Request(start, SeedData.GeneralLiftBayId, SeedData.TechAId);

        var created = await (await client.PostAsJsonAsync("/api/appointments", request))
            .Content.ReadFromJsonAsync<AppointmentResponse>();

        var cancel = await client.DeleteAsync($"/api/appointments/{created!.AppointmentId}");
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rebook = await client.PostAsJsonAsync("/api/appointments", request);
        rebook.StatusCode.Should().Be(HttpStatusCode.Created);

        var fetched = await client.GetAsync($"/api/appointments/{created.AppointmentId}");
        var cancelled = await fetched.Content.ReadFromJsonAsync<AppointmentResponse>();
        cancelled!.Status.Should().Be(nameof(AppointmentStatus.Cancelled));
    }

    [Fact]
    public async Task Post_returns_409_when_the_exact_slot_is_already_taken()
    {
        using var client = _factory.CreateClient();
        var start = TestCalendar.At(14, 9, 0);
        var request = Request(start, SeedData.GeneralLiftBayId, SeedData.TechAId);

        var first = await client.PostAsJsonAsync("/api/appointments", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/appointments", request);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Scheduling conflict");
    }

    [Fact]
    public async Task Cancelling_twice_is_rejected()
    {
        using var client = _factory.CreateClient();

        var created = await (await client.PostAsJsonAsync("/api/appointments", Request(TestCalendar.At(15, 9, 0))))
            .Content.ReadFromJsonAsync<AppointmentResponse>();

        (await client.DeleteAsync($"/api/appointments/{created!.AppointmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.DeleteAsync($"/api/appointments/{created.AppointmentId}");
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Booking_persists_relational_foreign_keys_the_database_can_validate()
    {
        using var client = _factory.CreateClient();
        var start = TestCalendar.At(16, 9, 0);

        var created = await (await client.PostAsJsonAsync("/api/appointments", Request(start)))
            .Content.ReadFromJsonAsync<AppointmentResponse>();

        var stored = await _factory.WithDbContextAsync(db =>
            db.Appointments.AsNoTracking().SingleAsync(a => a.Id == created!.AppointmentId));

        stored.ServiceBayId.Should().Be(created!.ServiceBayId);
        stored.TechnicianId.Should().Be(created.TechnicianId);
        stored.ServiceTypeId.Should().Be(created.ServiceTypeId);
        stored.CustomerId.Should().Be(SeedData.CustomerAId);
        stored.Status.Should().Be(AppointmentStatus.Scheduled);
        stored.VehicleIdentification.Value.Should().Be("1HGBH41JXMN109186");
    }
}
