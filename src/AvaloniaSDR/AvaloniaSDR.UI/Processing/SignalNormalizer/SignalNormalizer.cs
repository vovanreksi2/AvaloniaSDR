using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider;

namespace AvaloniaSDR.UI.Processing.SignalNormalizer;

public class SignalNormalizer : ISignalNormalizer
{
    private readonly double _range = SDRConstants.SignalPowerMax - SDRConstants.SignalPowerStart;

    public void Normalize(SignalDataPoint[] frame)
    {
        for (int i = 0; i < frame.Length; i++)
            frame[i].SignalPower = (frame[i].SignalPower - SDRConstants.SignalPowerStart) / _range;
    }
}
