using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider.Generators;
using System.Diagnostics;
using System.Threading.Channels;

namespace AvaloniaSDR.DataProvider.Providers;

public class SignalDataProvider(IDataGenerator dataGenerator) : IDataProvider, IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private readonly IDataGenerator dataGenerator = dataGenerator;

    public bool IsRunning => _worker != null && !_worker.IsCompleted;

    private readonly int updateIntervalInMs = 1000 / SDRConstants.UpdateRateHz;

    private readonly Channel<SignalDataPoint[]> channel = Channel.CreateBounded<SignalDataPoint[]>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ChannelReader<SignalDataPoint[]> Reader => channel.Reader;

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
        var stopwatch = Stopwatch.StartNew();
        var totalDuration = dataGenerator.TotalDuration;

        while (await timer.WaitForNextTickAsync(token))
        {
            var elapsed = stopwatch.Elapsed;

            if (totalDuration != TimeSpan.MaxValue && elapsed >= totalDuration)
            {
                return;
            }

            var data = (SignalDataPoint[])dataGenerator.GenerateData(elapsed).Clone();
            await channel.Writer.WriteAsync(data, token);
        } 
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRunning && _worker != null)
            await StopAsync();

        channel?.Writer?.Complete();

        _cts?.Dispose();
        _cts = null;
        _worker = null;
    }
}
