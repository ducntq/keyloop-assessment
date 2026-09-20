using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;

namespace KeyloopScheduler.Tests.Integration;

/// <summary>
/// Tier 2: availability search must only ever offer slots where BOTH a service bay
/// and a certified technician are free for the entire duration.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AvailabilityEndpointTests
{
    private static readonly Guid[] GeneralCertifiedTechnicians =
        { SeedData.TechAId, SeedData.TechBId, SeedData.TechCId };

    private readonly SchedulerApiFactory _factory;

    public AvailabilityEndpointTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    private Task<List<AvailabilitySlotResponse>?> GetSlotsAsync(Guid serviceTypeId, DateOnly date)
    {
        var client = _factory.CreateClient();
        return client.GetFromJsonAsync<List<AvailabilitySlotResponse>>(
            $"/api/availability?dealershipId={SeedData.DealershipId}&serviceTypeId={serviceTypeId}&date={date:yyyy-MM-dd}");
    }

    [Fact]
    public async Task Returns_only_technicians_that_hold_the_required_certification()
    {
        var slots = await GetSlotsAsync(SeedData.OilAndInspectionServiceTypeId, TestCalendar.Day(30));

        slots.Should().NotBeNullOrEmpty();
        var available = slots!;
        available.Select(s => s.TechnicianId).Distinct().Should().BeSubsetOf(GeneralCertifiedTechnicians);
        available.Select(s => s.TechnicianId).Should().NotContain(SeedData.TechDId);
    }

    [Fact]
    public async Task Ev_service_only_offers_the_single_ev_certified_technician()
    {
        var slots = await GetSlotsAsync(SeedData.EvBatteryDiagnosticsServiceTypeId, TestCalendar.Day(31));

        slots.Should().NotBeNullOrEmpty();
        slots!.Select(s => s.TechnicianId).Distinct().Should().Equal(SeedData.TechCId);
    }

    [Fact]
    public async Task Every_slot_has_the_service_duration_and_fits_operating_hours()
    {
        var slots = await GetSlotsAsync(SeedData.OilAndInspectionServiceTypeId, TestCalendar.Day(32));

        slots.Should().NotBeNullOrEmpty();
        slots!.Should().OnlyContain(slot =>
            slot.EndTimeUtc - slot.StartTimeUtc == TimeSpan.FromMinutes(30) &&
            slot.StartTimeUtc.TimeOfDay >= TimeSpan.FromHours(8) &&
            slot.EndTimeUtc.TimeOfDay <= TimeSpan.FromHours(18) &&
            slot.StartTimeUtc.Minute % 15 == 0 &&
            slot.StartTimeUtc.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task A_booked_slot_is_no_longer_offered()
    {
        var day = TestCalendar.Day(33);
        var start = TestCalendar.At(day, 9, 0);
        var target = new AvailabilitySlotResponse(SeedData.GeneralLiftBayId, SeedData.TechAId, start, start.AddMinutes(30));

        var before = await GetSlotsAsync(SeedData.OilAndInspectionServiceTypeId, day);
        before.Should().ContainEquivalentOf(target);

        using var client = _factory.CreateClient();
        var booking = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            ServiceBayId = SeedData.GeneralLiftBayId,
            TechnicianId = SeedData.TechAId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = start
        });
        booking.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await GetSlotsAsync(SeedData.OilAndInspectionServiceTypeId, day);

        after.Should().NotContainEquivalentOf(target);
        after.Should().NotContain(slot =>
            slot.ServiceBayId == SeedData.GeneralLiftBayId && slot.StartTimeUtc == start);
        after.Should().NotContain(slot =>
            slot.TechnicianId == SeedData.TechAId && slot.StartTimeUtc == start);
        after.Should().Contain(slot =>
            slot.ServiceBayId == SeedData.AlignmentRackBayId && slot.StartTimeUtc == start);
        after.Should().Contain(slot =>
            slot.TechnicianId == SeedData.TechBId && slot.StartTimeUtc == start);
    }

    [Fact]
    public async Task Unknown_service_type_returns_404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/availability?dealershipId={SeedData.DealershipId}&serviceTypeId={Guid.NewGuid()}&date={TestCalendar.Day(30):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
