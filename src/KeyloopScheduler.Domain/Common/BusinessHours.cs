namespace KeyloopScheduler.Domain.Common;

/// <summary>
/// Operating-hours and slot-quantization policy for the scheduler.
/// Kept as pure domain policy so it can be unit-tested without infrastructure.
/// </summary>
public static class BusinessHours
{
    /// <summary>Opening time of the service day (08:00 UTC).</summary>
    public static readonly TimeSpan OpenUtc = TimeSpan.FromHours(8);

    /// <summary>Closing time of the service day (18:00 UTC).</summary>
    public static readonly TimeSpan CloseUtc = TimeSpan.FromHours(18);

    /// <summary>Appointment starts must land on a 15-minute boundary.</summary>
    public const int QuantizationMinutes = 15;

    /// <summary>
    /// True when the whole window fits inside a single 08:00-18:00 UTC service day.
    /// </summary>
    public static bool IsWithinOperatingHours(TimeWindow window) =>
        window.StartUtc.TimeOfDay >= OpenUtc &&
        window.EndUtc.TimeOfDay <= CloseUtc &&
        window.StartUtc.Date == window.EndUtc.Date;

    /// <summary>
    /// True when the instant lands on a 15-minute boundary with no residual seconds.
    /// </summary>
    public static bool IsQuantized(DateTime utc) =>
        utc.Second == 0 &&
        utc.Millisecond == 0 &&
        utc.Microsecond == 0 &&
        utc.Minute % QuantizationMinutes == 0;
}
