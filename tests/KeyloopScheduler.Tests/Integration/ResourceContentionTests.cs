using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Tests.Integration;

/// <summary>
/// Tier 2: dual-resource contention. A booking is only valid when BOTH a bay and a
/// qualified technician are free; either resource being unavailable yields 409.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ResourceContentionTests
{
    private readonly SchedulerApiFactory _factory;

    public ResourceContentionTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Free_bay_but_no_certified_technician_returns_409()
    {
        // Tech C is the only EV-certified technician. Take them off shift: the bay
        // stays free, but the certification gate can no longer be satisfied.
        await SetTechnicianActiveAsync(SeedData.TechCId, isActive: false);

        try
        {
            var day = TestCalendar.Day(50);
            using var client = _factory.CreateClient();

            var availability = await client.GetFromJsonAsync<List<AvailabilitySlotResponse>>(
                $"/api/availability?dealershipId={SeedData.DealershipId}" +
                $"&serviceTypeId={SeedData.EvBatteryDiagnosticsServiceTypeId}&date={day:yyyy-MM-dd}");
            availability.Should().BeEmpty("no EV-certified technician is on shift");

            var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
            {
                DealershipId = SeedData.DealershipId,
                CustomerId = SeedData.CustomerAId,
                ServiceTypeId = SeedData.EvBatteryDiagnosticsServiceTypeId,
                Vin = "1HGBH41JXMN109186",
                StartTimeUtc = TestCalendar.At(day, 9, 0)
            });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Detail.Should().Contain("certified");
        }
        finally
        {
            await SetTechnicianActiveAsync(SeedData.TechCId, isActive: true);
        }
    }

    [Fact]
    public async Task Free_technician_but_bay_already_booked_returns_409()
    {
        var day = TestCalendar.Day(51);
        var start = TestCalendar.At(day, 10, 0);

        using var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = SeedData.GeneralLiftBayId,
            TechnicianId = SeedData.TechAId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = start
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Same bay and window, but a different (free) certified technician.
        var second = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = SeedData.GeneralLiftBayId,
            TechnicianId = SeedData.TechBId,
            Vin = "2T1BURHE0JC123456",
            StartTimeUtc = start
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Adjacent_bookings_that_touch_at_a_boundary_do_not_conflict()
    {
        var day = TestCalendar.Day(52);

        using var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = SeedData.AlignmentRackBayId,
            TechnicianId = SeedData.TechAId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = TestCalendar.At(day, 11, 0)
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Starts exactly when the previous appointment ends.
        var second = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = SeedData.AlignmentRackBayId,
            TechnicianId = SeedData.TechAId,
            Vin = "2T1BURHE0JC123456",
            StartTimeUtc = TestCalendar.At(day, 11, 30)
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private Task SetTechnicianActiveAsync(Guid technicianId, bool isActive) =>
        _factory.WithDbContextAsync(async db =>
        {
            var technician = await db.Technicians.SingleAsync(t => t.Id == technicianId);

            if (isActive)
            {
                technician.Activate();
            }
            else
            {
                technician.Deactivate();
            }

            await db.SaveChangesAsync();
        });
}
