using FluentAssertions;
using KeyloopScheduler.Domain.Common;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: operating-hours and 15-minute quantization policy.
/// </summary>
[Trait("Category", "Domain")]
public sealed class BusinessHoursTests
{
    private static readonly DateOnly Day = new(2026, 3, 2);

    private static TimeWindow Window(int startHour, int startMinute, int endHour, int endMinute) =>
        new(
            Day.ToDateTime(new TimeOnly(startHour, startMinute), DateTimeKind.Utc),
            Day.ToDateTime(new TimeOnly(endHour, endMinute), DateTimeKind.Utc));

    [Theory]
    [InlineData(8, 0, 8, 30)]
    [InlineData(8, 0, 18, 0)]
    [InlineData(17, 30, 18, 0)]
    [InlineData(12, 0, 13, 0)]
    public void Window_inside_operating_hours_is_accepted(int sh, int sm, int eh, int em)
    {
        BusinessHours.IsWithinOperatingHours(Window(sh, sm, eh, em)).Should().BeTrue();
    }

    [Theory]
    [InlineData(7, 45, 8, 15)]
    [InlineData(17, 0, 18, 15)]
    [InlineData(17, 30, 19, 0)]
    public void Window_breaching_operating_hours_is_rejected(int sh, int sm, int eh, int em)
    {
        BusinessHours.IsWithinOperatingHours(Window(sh, sm, eh, em)).Should().BeFalse();
    }

    [Fact]
    public void Window_crossing_midnight_is_rejected()
    {
        var start = Day.ToDateTime(new TimeOnly(23, 0), DateTimeKind.Utc);
        var end = Day.AddDays(1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc);

        BusinessHours.IsWithinOperatingHours(new TimeWindow(start, end)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void Start_on_quarter_hour_is_quantized(int minute)
    {
        var instant = Day.ToDateTime(new TimeOnly(9, minute), DateTimeKind.Utc);

        BusinessHours.IsQuantized(instant).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(59)]
    public void Start_off_quarter_hour_is_not_quantized(int minute)
    {
        var instant = Day.ToDateTime(new TimeOnly(9, minute), DateTimeKind.Utc);

        BusinessHours.IsQuantized(instant).Should().BeFalse();
    }

    [Fact]
    public void Start_with_residual_seconds_is_not_quantized()
    {
        var instant = Day.ToDateTime(new TimeOnly(9, 15), DateTimeKind.Utc).AddSeconds(1);

        BusinessHours.IsQuantized(instant).Should().BeFalse();
    }
}
