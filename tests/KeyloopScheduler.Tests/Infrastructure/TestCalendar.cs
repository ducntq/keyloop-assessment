namespace KeyloopScheduler.Tests.Infrastructure;

/// <summary>
/// Deterministic UTC calendar helpers. Every date produced is in the future so it
/// satisfies the "no bookings in the past" invariant.
/// </summary>
internal static class TestCalendar
{
    public static DateOnly Day(int offsetDaysFromToday) =>
        DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(offsetDaysFromToday));

    public static DateTime At(DateOnly day, int hour, int minute = 0) =>
        day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc);

    public static DateTime At(int offsetDaysFromToday, int hour, int minute = 0) =>
        At(Day(offsetDaysFromToday), hour, minute);
}
