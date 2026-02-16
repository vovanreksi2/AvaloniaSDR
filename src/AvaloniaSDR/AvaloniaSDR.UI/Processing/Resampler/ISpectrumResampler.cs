using AvaloniaSDR.DataProvider;
using System;

namespace AvaloniaSDR.UI.Processing.Resampler;

public interface ISpectrumResampler
{
    /// <summary>Resample <paramref name="input"/> signal points into <paramref name="output"/> pixel slots.</summary>
    void Resample(ReadOnlySpan<SignalDataPoint> input, Span<double> output);
}
