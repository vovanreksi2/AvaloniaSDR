namespace AvaloniaSDR.DataProvider.Providers;

public interface IDataProvider
{
    bool IsRunning { get; }
    
    event DataGeneratedEventHandler? DataGenerated;

    void Start();
    Task StopAsync();
}