using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Domain.Common;

/// <summary>
/// Immutable, validated interval of time expressed strictly in UTC.
/// All interval math (overlap, adjacency, containment, duration) lives here so that
/// the rest of the domain never re-implements it.
/// </summary>
public readonly record struct TimeWindow
{
    public DateTime StartUtc { get; }

    public DateTime EndUtc { get; }

    public TimeWindow(DateTime startUtc, DateTime endUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException(
                $"Start time must be UTC. Received kind '{startUtc.Kind}'.");
        }

        if (endUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException(
                $"End time must be UTC. Received kind '{endUtc.Kind}'.");
        }

        if (endUtc <= startUtc)
        {
            throw new DomainValidationException(
                "End time must be strictly after start time.");
        }

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public TimeSpan Duration => EndUtc - StartUtc;

    /// <summary>
    /// Half-open overlap test. Adjacent windows that merely touch at a boundary
    /// (e.g. 09:00-09:30 and 09:30-10:00) do NOT overlap.
    /// </summary>
    public bool OverlapsWith(TimeWindow other) =>
        StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Contains(DateTime instantUtc) =>
        instantUtc >= StartUtc && instantUtc < EndUtc;

    /// <summary>True when the two windows share exactly one boundary and no interior.</summary>
    public bool IsAdjacentTo(TimeWindow other) =>
        EndUtc == other.StartUtc || other.EndUtc == StartUtc;

    /// <summary>True when this window fully encloses <paramref name="other"/>.</summary>
    public bool Encloses(TimeWindow other) =>
        StartUtc <= other.StartUtc && other.EndUtc <= EndUtc;
}
