using AvaloniaSDR.Constants;
using MathNet.Numerics.Distributions;

namespace AvaloniaSDR.DataProvider.Generators;

/// <summary>
/// Generates a noise floor across all frequency points.
/// Model: <c>NoiseBaseLevel + Normal(0,1).Sample() * NoiseRandomLevel</c>
/// The <paramref name="elapsed"/> parameter is accepted but ignored — noise is stateless.
/// </summary>
internal sealed class NoiseDataGenerator : IDataGenerator
{
    private readonly Normal _normal = new(0, 1);
    private readonly double _frequencyStep =
        (SDRConstants.FrequencyEnd - SDRConstants.FrequencyStart) / SDRConstants.Points;
    private readonly SignalDataPoint[] _frame = new SignalDataPoint[SDRConstants.Points];

    public TimeSpan TotalDuration => TimeSpan.MaxValue;

    public SignalDataPoint[] GenerateData(TimeSpan elapsed)
    {
        for (var i = 0; i < SDRConstants.Points; i++)
        {
            _frame[i].Frequency = SDRConstants.FrequencyStart + (i * _frequencyStep);
            _frame[i].SignalPower = SDRConstants.NoiseBaseLevel + _normal.Sample() * SDRConstants.NoiseRandomLevel;
        }
        return _frame;
    }
}
