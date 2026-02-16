namespace AvaloniaSDR.DataProvider;

/// <summary>
/// Describes a single temporal power segment for a time-varying signal.
/// </summary>
/// <param name="Duration">
///   How long this segment lasts. Use <see cref="TimeSpan.MaxValue"/> for an infinite last segment.
/// </param>
/// <param name="Power">
///   The signal amplitude at the Gaussian peak during this segment (additive units).
/// </param>
public record SignalSegment(TimeSpan Duration, double Power);
