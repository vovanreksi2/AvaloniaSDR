using AvaloniaSDR.Constants;

namespace AvaloniaSDR.DataProvider.Generators;

/// <summary>
/// Combines the outputs of N child generators by element-wise addition of <c>SignalPower</c>.
/// <para>
/// The first generator (typically <see cref="NoiseDataGenerator"/>) provides the base frame
/// including frequency values. Each subsequent generator's <c>SignalPower</c> is added on top.
/// </para>
/// </summary>
internal sealed class CompositeDataGenerator : IDataGenerator
{
    private readonly IReadOnlyList<IDataGenerator> _generators;
    private readonly SignalDataPoint[] _output = new SignalDataPoint[SDRConstants.Points];

    public TimeSpan TotalDuration { get; }

    public CompositeDataGenerator(IReadOnlyList<IDataGenerator> generators, TimeSpan totalDuration)
    {
        _generators = generators;
        TotalDuration = totalDuration;
    }

    public SignalDataPoint[] GenerateData(TimeSpan elapsed)
    {
        // First generator (noise) provides the base — copy its frame into output
        var baseFrame = _generators[0].GenerateData(elapsed);
        for (var i = 0; i < SDRConstants.Points; i++)
            _output[i] = baseFrame[i];

        // Add each subsequent generator's signal contribution
        for (var g = 1; g < _generators.Count; g++)
        {
            var childFrame = _generators[g].GenerateData(elapsed);
            for (var i = 0; i < SDRConstants.Points; i++)
                _output[i].SignalPower += childFrame[i].SignalPower;
        }

        return _output;
    }
}
