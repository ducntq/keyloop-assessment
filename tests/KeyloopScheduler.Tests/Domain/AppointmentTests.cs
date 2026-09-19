using FluentAssertions;
using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Entities;
using KeyloopScheduler.Domain.Enums;
using KeyloopScheduler.Domain.Exceptions;
using KeyloopScheduler.Domain.ValueObjects;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: appointment lifecycle, state transitions and bookable-window guards.
/// </summary>
[Trait("Category", "Domain")]
public sealed class AppointmentTests
{
    private static readonly Guid AppointmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DealershipId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BayId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TechnicianId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ServiceTypeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Vin Vehicle = new("1HGBH41JXMN109186");
    private static readonly DateOnly Day = new(2026, 3, 2);
    private static readonly DateTime CreatedAt = new(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TimeWindow Window(int hour, int minute, int durationMinutes) =>
        new(
            Day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc),
            Day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc).AddMinutes(durationMinutes));

    private static Appointment Schedule(TimeWindow window, DateTime? createdAtUtc = null) =>
        Appointment.Schedule(
            AppointmentId,
            DealershipId,
            BayId,
            TechnicianId,
            ServiceTypeId,
            Vehicle,
            window,
            createdAtUtc ?? CreatedAt);

    [Fact]
    public void Schedule_creates_a_scheduled_appointment_that_occupies_resources()
    {
        var appointment = Schedule(Window(9, 0, 30));

        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        appointment.OccupiesResources.Should().BeTrue();
        appointment.StartTimeUtc.Should().Be(Window(9, 0, 30).StartUtc);
        appointment.EndTimeUtc.Should().Be(Window(9, 0, 30).EndUtc);
        appointment.Window.Duration.Should().Be(TimeSpan.FromMinutes(30));
        appointment.VehicleIdentification.Should().Be(Vehicle);
        appointment.CancelledAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(9, 7)]
    [InlineData(9, 1)]
    [InlineData(9, 59)]
    public void Schedule_rejects_starts_that_are_not_quantized(int hour, int minute)
    {
        var act = () => Schedule(Window(hour, minute, 30));

        act.Should().Throw<DomainValidationException>().WithMessage("*quantized*");
    }

    [Theory]
    [InlineData(7, 45, 30)]
    [InlineData(17, 30, 90)]
    public void Schedule_rejects_windows_outside_operating_hours(int hour, int minute, int duration)
    {
        var act = () => Schedule(Window(hour, minute, duration));

        act.Should().Throw<DomainValidationException>().WithMessage("*operating hours*");
    }

    [Fact]
    public void Schedule_rejects_starts_in_the_past()
    {
        var window = Window(9, 0, 30);
        var createdAtAfterStart = window.StartUtc.AddMinutes(1);

        var act = () => Schedule(window, createdAtAfterStart);

        act.Should().Throw<DomainValidationException>().WithMessage("*must not be in the past*");
    }

    [Fact]
    public void Schedule_rejects_empty_resource_identifiers()
    {
        var act = () => Appointment.Schedule(
            Guid.Empty,
            DealershipId,
            BayId,
            TechnicianId,
            ServiceTypeId,
            Vehicle,
            Window(9, 0, 30),
            CreatedAt);

        act.Should().Throw<DomainValidationException>().WithMessage("*Appointment id*");
    }

    [Fact]
    public void Schedule_rejects_non_utc_created_at()
    {
        var act = () => Appointment.Schedule(
            AppointmentId,
            DealershipId,
            BayId,
            TechnicianId,
            ServiceTypeId,
            Vehicle,
            Window(9, 0, 30),
            new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Unspecified));

        act.Should().Throw<DomainValidationException>().WithMessage("*Created-at*UTC*");
    }

    [Fact]
    public void Cancel_moves_to_cancelled_and_releases_resources()
    {
        var appointment = Schedule(Window(9, 0, 30));
        var cancelledAt = CreatedAt.AddHours(1);

        appointment.Cancel(cancelledAt);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.OccupiesResources.Should().BeFalse();
        appointment.CancelledAtUtc.Should().Be(cancelledAt);
    }

    [Fact]
    public void Cancel_is_rejected_when_already_cancelled()
    {
        var appointment = Schedule(Window(9, 0, 30));
        appointment.Cancel(CreatedAt);

        var act = () => appointment.Cancel(CreatedAt.AddMinutes(5));

        act.Should().Throw<DomainValidationException>().WithMessage("*already cancelled*");
    }

    [Fact]
    public void Cancel_is_rejected_for_a_completed_appointment()
    {
        var appointment = Schedule(Window(9, 0, 30));
        appointment.Complete();

        var act = () => appointment.Cancel(CreatedAt);

        act.Should().Throw<DomainValidationException>().WithMessage("*completed*");
    }

    [Fact]
    public void Reschedule_moves_a_scheduled_appointment()
    {
        var appointment = Schedule(Window(9, 0, 30));

        appointment.Reschedule(Window(14, 15, 60));

        appointment.StartTimeUtc.Should().Be(Window(14, 15, 60).StartUtc);
        appointment.EndTimeUtc.Should().Be(Window(14, 15, 60).EndUtc);
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
    }

    [Fact]
    public void Reschedule_is_rejected_for_a_cancelled_appointment()
    {
        var appointment = Schedule(Window(9, 0, 30));
        appointment.Cancel(CreatedAt);

        var act = () => appointment.Reschedule(Window(14, 15, 60));

        act.Should().Throw<DomainValidationException>().WithMessage("*Only scheduled appointments*");
    }

    [Fact]
    public void Reschedule_still_enforces_operating_hours()
    {
        var appointment = Schedule(Window(9, 0, 30));

        var act = () => appointment.Reschedule(Window(17, 30, 90));

        act.Should().Throw<DomainValidationException>().WithMessage("*operating hours*");
    }

    [Fact]
    public void Complete_only_applies_to_scheduled_appointments()
    {
        var appointment = Schedule(Window(9, 0, 30));
        appointment.Complete();

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.OccupiesResources.Should().BeFalse();

        var act = () => appointment.Complete();
        act.Should().Throw<DomainValidationException>();
    }
}
