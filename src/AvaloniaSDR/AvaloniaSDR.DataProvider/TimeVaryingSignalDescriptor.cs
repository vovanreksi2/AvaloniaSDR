namespace AvaloniaSDR.DataProvider;

/// <summary>
/// Describes a Gaussian signal peak whose power changes piecewise over time.
/// </summary>
/// <param name="CenterFrequencyMHz">Center of the Gaussian peak in MHz.</param>
/// <param name="WidthMHz">Standard deviation of the Gaussian in MHz.</param>
/// <param name="Segments">Ordered list of (duration, power) time segments.</param>
/// <param name="Loop">If true, the segment list repeats cyclically after the last segment ends.</param>
public record TimeVaryingSignalDescriptor(
    double CenterFrequencyMHz,
    double WidthMHz,
    IReadOnlyList<SignalSegment> Segments,
    bool Loop = false)
{
    /// <summary>
    /// Total duration of one pass through all segments.
    /// Returns <see cref="TimeSpan.MaxValue"/> if any segment has an infinite duration.
    /// </summary>
    public TimeSpan TotalDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var seg in Segments)
            {
                if (seg.Duration == TimeSpan.MaxValue)
                    return TimeSpan.MaxValue;
                total += seg.Duration;
            }
            return total;
        }
    }

    /// <summary>
    /// Returns the signal power for the given elapsed simulation time.
    /// <list type="bullet">
    ///   <item>If <see cref="Loop"/> is false and elapsed ≥ <see cref="TotalDuration"/>, returns 0 (signal gone).</item>
    ///   <item>If <see cref="Loop"/> is true, wraps elapsed using modulo of total duration.</item>
    /// </list>
    /// </summary>
    public double ResolvePower(TimeSpan elapsed)
    {
        var total = TotalDuration;

        if (!Loop && elapsed >= total)
            return 0.0;

        TimeSpan effective;
        if (Loop && total != TimeSpan.MaxValue)
            effective = TimeSpan.FromTicks(elapsed.Ticks % total.Ticks);
        else
            effective = elapsed;

        return WalkSegments(effective);
    }

    private double WalkSegments(TimeSpan effective)
    {
        var cursor = TimeSpan.Zero;
        foreach (var seg in Segments)
        {
            cursor += seg.Duration;
            if (effective < cursor)
                return seg.Power;
        }
        return 0.0;
    }
}
