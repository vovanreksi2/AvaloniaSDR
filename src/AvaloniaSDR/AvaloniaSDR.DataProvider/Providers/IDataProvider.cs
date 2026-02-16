using System.Threading.Channels;

namespace AvaloniaSDR.DataProvider.Providers;

public interface IDataProvider
{
    bool IsRunning { get; }
    
    public ChannelReader<SignalDataPoint[]> Reader { get; }

    void Start();
    Task StopAsync();
}