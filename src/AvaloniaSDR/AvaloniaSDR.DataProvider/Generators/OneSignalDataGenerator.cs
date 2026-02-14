using AvaloniaSDR.Constants;

namespace AvaloniaSDR.DataProvider.Generators;

internal class OneSignalDataGenerator : IDataGenerator
{
    private readonly Random random = new();
    private readonly double frequencyStep; 
    private readonly SignalDataPoint[] frame = new SignalDataPoint[SDRConstants.Points];

    public OneSignalDataGenerator()
    {
        frequencyStep = CalculateFrequencyStep(SDRConstants.FrequencyStart, SDRConstants.FrequencyEnd);
    }

    public SignalDataPoint[] GenerateData()
    {
        for (int i = 0; i < SDRConstants.Points; i++)
        {
            var frequency = SDRConstants.FrequencyStart + (i * frequencyStep);

            var noise = GenerateNoise();

            var signal = GenerateSignal(frequency);
            frame[i].Frequency = frequency;
            frame[i].SignalPower = noise + signal;
        }
        return frame;
    }

    private double GenerateNoise() => SDRConstants.NoiseBaseLevel + NextGaussian() * SDRConstants.NoiseRandomLevel;

    private double GenerateSignal(double frequency)
    {
        var delta = frequency - SDRConstants.SignalCenterFrequencyMHz;
        var signal = SDRConstants.SignalPower *
            Math.Exp(-(delta * delta) / (2 * SDRConstants.SignalWidthMHz * SDRConstants.SignalWidthMHz));
        return signal;
    }

    private double NextGaussian()
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) *
            Math.Sin(2.0 * Math.PI * u2);
    }

    private double CalculateFrequencyStep(double frequencyStart, double frequencyEnd)
    {
        var frequencyRange = frequencyEnd - frequencyStart;
        return frequencyRange / SDRConstants.Points;
    }
}