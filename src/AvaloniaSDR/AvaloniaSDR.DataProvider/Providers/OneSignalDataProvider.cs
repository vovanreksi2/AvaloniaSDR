using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider.Generators;
using System.Threading.Channels;

namespace AvaloniaSDR.DataProvider.Providers;

public delegate void DataGeneratedEventHandler(IEnumerable<SignalDataPoint> data);

public class OneSignalDataProvider(IDataGenerator dataGenerator) : IDataProvider, IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private readonly IDataGenerator dataGenerator = dataGenerator;

    public event DataGeneratedEventHandler? DataGenerated;

    public bool IsRunning => _worker != null && !_worker.IsCompleted;

    private int updateIntervalInMs = 1000 / SDRConstants.UpdateIntervalInSec;

    private readonly Channel<SignalDataPoint[]> _channel = Channel.CreateBounded<SignalDataPoint[]>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ChannelReader<SignalDataPoint[]> Reader => _channel.Reader;

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();

        _worker = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        _cts.Cancel();

        try
        {
            if (_worker == null) return;

            await _worker;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _worker = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(updateIntervalInMs));

        while (await timer.WaitForNextTickAsync(token))
        {
            var data = dataGenerator.GenerateData();
            await _channel.Writer.WriteAsync(data, token);
        }
    }

    private async Task GenerateData()
    {
       
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRunning && _worker != null) 
            await StopAsync();

        _cts?.Dispose();
        _cts = null;
        _worker = null;
    }
}
