using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KeyloopScheduler.Tests.Concurrency;

/// <summary>
/// Tier 3: multi-client race simulation. The catalogue is reduced to exactly one
/// service bay and one qualified technician, then simultaneous requests are fired
/// for the identical slot. Exactly one may win; the loser must receive a conflict.
/// </summary>
[Collection(ConcurrencyCollection.Name)]
[Trait("Category", "Concurrency")]
public sealed class DoubleBookingRaceTests
{
    private readonly SchedulerApiFactory _factory;

    public DoubleBookingRaceTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    private CreateAppointmentRequest Request(DateTime start) =>
        new()
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = start
        };

    /// <summary>Reduces the dealership to a single bay and a single qualified technician.</summary>
    private async Task TrimToSingleResourcePairAsync()
    {
        await _factory.WithDbContextAsync(async db =>
        {
            await db.Appointments.ExecuteDeleteAsync();
            await db.Technicians.Where(t => t.Id != SeedData.TechAId).ExecuteDeleteAsync();
            await db.ServiceBays.Where(b => b.Id != SeedData.GeneralLiftBayId).ExecuteDeleteAsync();
        });

        var bays = await _factory.WithDbContextAsync(db => db.ServiceBays.CountAsync());
        var technicians = await _factory.WithDbContextAsync(db => db.Technicians.CountAsync());

        bays.Should().Be(1);
        technicians.Should().Be(1);
    }

    [Fact]
    public async Task Two_simultaneous_requests_for_the_same_slot_produce_exactly_one_winner()
    {
        await TrimToSingleResourcePairAsync();

        var start = TestCalendar.At(60, 9, 0);
        var request = Request(start);

        using var firstClient = _factory.CreateClient();
        using var secondClient = _factory.CreateClient();

        var firstTask = firstClient.PostAsJsonAsync("/api/appointments", request);
        var secondTask = secondClient.PostAsJsonAsync("/api/appointments", request);

        var responses = await Task.WhenAll(firstTask, secondTask);
        var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(code => code).ToArray();

        statuses.Should().Equal(
            [(int)HttpStatusCode.Created, (int)HttpStatusCode.Conflict],
            "exactly one client may reserve the only bay/technician pair");

        var scheduled = await _factory.WithDbContextAsync(db =>
            db.Appointments.CountAsync(a =>
                a.StartTimeUtc == start && a.Status == AppointmentStatus.Scheduled));
        scheduled.Should().Be(1);

        var total = await _factory.WithDbContextAsync(db => db.Appointments.CountAsync());
        total.Should().Be(1);
    }

    [Fact]
    public async Task Repeated_races_never_double_book_the_single_resource_pair()
    {
        await TrimToSingleResourcePairAsync();

        const int rounds = 10;
        const int contenders = 5;
        var starts = Enumerable.Range(0, rounds).Select(round => TestCalendar.At(61, 8, 0).AddMinutes(round * 30)).ToList();

        foreach (var start in starts)
        {
            var request = Request(start);

            var clients = Enumerable.Range(0, contenders).Select(_ => _factory.CreateClient()).ToList();
            var responses = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync("/api/appointments", request)));

            foreach (var client in clients)
            {
                client.Dispose();
            }

            var statuses = responses.Select(r => (int)r.StatusCode).OrderBy(code => code).ToArray();
            statuses.Count(code => code == (int)HttpStatusCode.Created).Should().Be(1);
            statuses.Count(code => code == (int)HttpStatusCode.Conflict).Should().Be(contenders - 1);
            statuses.Should().OnlyContain(code =>
                code == (int)HttpStatusCode.Created || code == (int)HttpStatusCode.Conflict);
        }

        var total = await _factory.WithDbContextAsync(db => db.Appointments.CountAsync());
        total.Should().Be(rounds, "every round must create exactly one appointment");

        var duplicateWindows = await _factory.WithDbContextAsync(db =>
            db.Appointments
                .Where(a => a.Status == AppointmentStatus.Scheduled)
                .GroupBy(a => new { a.ServiceBayId, a.StartTimeUtc })
                .CountAsync(group => group.Count() > 1));

        duplicateWindows.Should().Be(0);
    }
}
