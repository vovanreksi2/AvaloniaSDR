using AvaloniaSDR.DataProvider;

namespace AvaloniaSDR.UI.Processing.SignalNormalizer;

public interface ISignalNormalizer
{
    void Normalize(SignalDataPoint[] frame);
}
