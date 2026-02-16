using AvaloniaSDR.Constants;

namespace AvaloniaSDR.DataProvider.Generators;

/// <summary>
/// Generates the contribution of a single time-varying Gaussian signal peak.
/// Produces ONLY the signal delta above zero — noise is handled by <see cref="NoiseDataGenerator"/>.
/// <para>
/// Signal model (identical to original <c>OneSignalDataGenerator</c>):
/// <c>contribution_i = currentPower * Exp(-(delta_i²) / (2*width²)) + Random(-5,5)</c>
/// where <c>delta_i = frequency_i - CenterFrequencyMHz</c>.
/// </para>
/// When <c>currentPower == 0</c> (signal off-air), all contributions are 0.
/// </summary>
internal sealed class SignalDataGenerator : IDataGenerator
{
    private readonly TimeVaryingSignalDescriptor _descriptor;
    private readonly Random _random = new();
    private readonly double _frequencyStep =
        (SDRConstants.FrequencyEnd - SDRConstants.FrequencyStart) / SDRConstants.Points;
    private readonly SignalDataPoint[] _frame = new SignalDataPoint[SDRConstants.Points];
    private readonly double _twoSigmaSquared;
    private readonly double _cutoffDistance; // 4σ — Gaussian < 0.00034 beyond this point

    public TimeSpan TotalDuration => _descriptor.TotalDuration;

    public SignalDataGenerator(TimeVaryingSignalDescriptor descriptor)
    {
        _descriptor = descriptor;
        _twoSigmaSquared = 2 * descriptor.WidthMHz * descriptor.WidthMHz;
        _cutoffDistance = 4 * descriptor.WidthMHz;
    }

    public SignalDataPoint[] GenerateData(TimeSpan elapsed)
    {
        var currentPower = _descriptor.ResolvePower(elapsed);

        for (var i = 0; i < SDRConstants.Points; i++)
        {
            var frequency = SDRConstants.FrequencyStart + (i * _frequencyStep);
            _frame[i].Frequency = frequency;

            if (currentPower == 0.0)
            {
                _frame[i].SignalPower = 0.0;
                continue;
            }

            var delta = frequency - _descriptor.CenterFrequencyMHz;
            if (Math.Abs(delta) > _cutoffDistance)
            {
                _frame[i].SignalPower = 0.0;
                continue;
            }

            _frame[i].SignalPower =
                currentPower * Math.Exp(-(delta * delta) / _twoSigmaSquared)
                + _random.Next(-5, 5);
        }

        return _frame;
    }
}
