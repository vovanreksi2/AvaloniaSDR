using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSDR.DataProvider.Processing;

/// <summary>Delegates to <see cref="MaxHoldDownsampler"/> when downsampling and <see cref="LinearUpsamplingResampler"/> when upsampling.</summary>
public sealed class AdaptiveSpectrumResampler : ISpectrumResampler
{
    private readonly ISpectrumResampler _down;
    private readonly ISpectrumResampler _up;

    public AdaptiveSpectrumResampler(
        [FromKeyedServices(SpectrumResamplerKeys.Down)] ISpectrumResampler down,
        [FromKeyedServices(SpectrumResamplerKeys.Up)] ISpectrumResampler up)
    {
        _down = down;
        _up = up;
    }

    public void Resample(ReadOnlySpan<SignalDataPoint> input, Span<double> output)
    {
        if (input.Length > output.Length)
        {
            _down.Resample(input, output);
        }
        else
        {
            _up.Resample(input, output);
        }
    }
}
