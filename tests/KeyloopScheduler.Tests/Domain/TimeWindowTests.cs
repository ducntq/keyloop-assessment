using FluentAssertions;
using KeyloopScheduler.Domain.Common;
using KeyloopScheduler.Domain.Exceptions;

namespace KeyloopScheduler.Tests.Domain;

/// <summary>
/// Tier 1: the interval-overlap matrix that every booking decision rests on.
/// </summary>
[Trait("Category", "Domain")]
public sealed class TimeWindowTests
{
    private static readonly DateTime Base = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    private static TimeWindow Window(int startMinutes, int endMinutes) =>
        new(Base.AddMinutes(startMinutes), Base.AddMinutes(endMinutes));

    [Fact]
    public void Duration_is_difference_between_bounds()
    {
        Window(0, 90).Duration.Should().Be(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void Adjacent_windows_that_touch_at_a_boundary_do_not_overlap()
    {
        var first = Window(0, 30);
        var second = Window(30, 60);

        first.OverlapsWith(second).Should().BeFalse();
        second.OverlapsWith(first).Should().BeFalse();
        first.IsAdjacentTo(second).Should().BeTrue();
    }

    [Fact]
    public void Partially_overlapping_windows_overlap()
    {
        Window(0, 30).OverlapsWith(Window(15, 45)).Should().BeTrue();
    }

    [Fact]
    public void Window_fully_inside_another_overlaps()
    {
        var outer = Window(0, 90);
        var inner = Window(30, 60);

        outer.OverlapsWith(inner).Should().BeTrue();
        inner.OverlapsWith(outer).Should().BeTrue();
        outer.Encloses(inner).Should().BeTrue();
        inner.Encloses(outer).Should().BeFalse();
    }

    [Fact]
    public void Identical_windows_overlap()
    {
        var window = Window(0, 30);

        window.OverlapsWith(Window(0, 30)).Should().BeTrue();
    }

    [Fact]
    public void Disjoint_windows_do_not_overlap()
    {
        Window(0, 30).OverlapsWith(Window(60, 90)).Should().BeFalse();
    }

    [Fact]
    public void Contains_uses_half_open_semantics()
    {
        var window = Window(0, 30);

        window.Contains(Base).Should().BeTrue();
        window.Contains(Base.AddMinutes(29)).Should().BeTrue();
        window.Contains(Base.AddMinutes(30)).Should().BeFalse();
        window.Contains(Base.AddMinutes(-1)).Should().BeFalse();
    }

    [Fact]
    public void Non_utc_bounds_are_rejected()
    {
        var unspecified = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

        var startAct = () => new TimeWindow(unspecified, utc);
        var endAct = () => new TimeWindow(utc, unspecified);

        startAct.Should().Throw<DomainValidationException>().WithMessage("*Start time must be UTC*");
        endAct.Should().Throw<DomainValidationException>().WithMessage("*End time must be UTC*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void End_must_be_strictly_after_start(int endOffsetMinutes)
    {
        var act = () => new TimeWindow(Base, Base.AddMinutes(endOffsetMinutes));

        act.Should().Throw<DomainValidationException>().WithMessage("*strictly after*");
    }
}
