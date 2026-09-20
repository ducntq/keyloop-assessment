using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Infrastructure.Persistence;
using KeyloopScheduler.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace KeyloopScheduler.Tests.Integration;

/// <summary>
/// Tier 2: input validation contracts. Non-UTC timestamps, past dates, malformed
/// VINs, out-of-hours and non-quantized starts must all be rejected as 400.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ValidationEndpointTests
{
    private readonly SchedulerApiFactory _factory;

    public ValidationEndpointTests(SchedulerApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateAppointmentRequest Request(DateTime start, string vin = "1HGBH41JXMN109186") =>
        new()
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            Vin = vin,
            StartTimeUtc = start
        };

    private async Task<ProblemDetails> PostExpectingFailureAsync(CreateAppointmentRequest request, HttpStatusCode expected)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments", request);

        response.StatusCode.Should().Be(expected);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)expected);
        return problem;
    }

    [Fact]
    public async Task Non_utc_start_is_rejected()
    {
        var unspecified = new DateTime(2026, 5, 4, 9, 0, 0, DateTimeKind.Unspecified);

        var problem = await PostExpectingFailureAsync(Request(unspecified), HttpStatusCode.BadRequest);

        problem.Detail.Should().Contain("UTC");
    }

    [Fact]
    public async Task Past_start_is_rejected()
    {
        var past = DateTime.UtcNow.Date.AddDays(-7).AddHours(9);

        var problem = await PostExpectingFailureAsync(Request(past), HttpStatusCode.BadRequest);

        problem.Detail.Should().Contain("past");
    }

    [Fact]
    public async Task Malformed_vin_is_rejected_by_the_domain()
    {
        // 17 characters, but contains the forbidden letter I.
        var problem = await PostExpectingFailureAsync(
            Request(TestCalendar.At(40, 9, 0), "1HGBH41JXMN109I86"),
            HttpStatusCode.BadRequest);

        problem.Detail.Should().Contain("VIN");
    }

    [Fact]
    public async Task Short_vin_is_rejected_by_contract_validation()
    {
        var response = await PostExpectingFailureAsync(
            Request(TestCalendar.At(41, 9, 0), "TOO-SHORT"),
            HttpStatusCode.BadRequest);

        response.Title.Should().Contain("validation errors");
    }

    [Fact]
    public async Task Non_quantized_start_is_rejected()
    {
        var problem = await PostExpectingFailureAsync(
            Request(TestCalendar.At(42, 9, 7)),
            HttpStatusCode.BadRequest);

        problem.Detail.Should().Contain("quantized");
    }

    [Fact]
    public async Task Service_running_past_closing_time_is_rejected()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.EvBatteryDiagnosticsServiceTypeId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = TestCalendar.At(43, 17, 30)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("operating hours");
    }

    [Fact]
    public async Task Unknown_dealership_returns_404()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = Guid.NewGuid(),
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.OilAndInspectionServiceTypeId,
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = TestCalendar.At(44, 9, 0)
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Requesting_a_technician_without_the_required_certification_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            DealershipId = SeedData.DealershipId,
            CustomerId = SeedData.CustomerAId,
            ServiceTypeId = SeedData.BrakePadReplacementServiceTypeId,
            TechnicianId = SeedData.TechAId, // GENERAL only, brakes require BRAKES
            Vin = "1HGBH41JXMN109186",
            StartTimeUtc = TestCalendar.At(45, 9, 0)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("Brakes");
    }
}
