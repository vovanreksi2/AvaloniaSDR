using Avalonia.Threading;
using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Providers;
using AvaloniaSDR.UI.Diagnostics;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AvaloniaSDR.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IDataProvider dataProvider;
    private readonly FrameMetrics? _frameMetrics;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    private SignalDataPoint[]? spectrumData;
    public SignalDataPoint[]? SpectrumData
    {
        get => spectrumData;
        set =>
            this.RaiseAndSetIfChanged(ref spectrumData, value);
    }

    private long frameVersion;
    public long FrameVersion
    {
        get => frameVersion;
        set => this.RaiseAndSetIfChanged(ref frameVersion, value);
    }

    private double _currentFps;
    public double CurrentFps
    {
        get => _currentFps;
        set => this.RaiseAndSetIfChanged(ref _currentFps, value);
    }

    private double _avgFrameTimeMs;
    public double AvgFrameTimeMs
    {
        get => _avgFrameTimeMs;
        set => this.RaiseAndSetIfChanged(ref _avgFrameTimeMs, value);
    }

    private double _minFrameTimeMs;
    public double MinFrameTimeMs
    {
        get => _minFrameTimeMs;
        set => this.RaiseAndSetIfChanged(ref _minFrameTimeMs, value);
    }

    private double _maxFrameTimeMs;
    public double MaxFrameTimeMs
    {
        get => _maxFrameTimeMs;
        set => this.RaiseAndSetIfChanged(ref _maxFrameTimeMs, value);
    }

    private int _freezeCount;
    public int FreezeCount
    {
        get => _freezeCount;
        set => this.RaiseAndSetIfChanged(ref _freezeCount, value);
    }

    private bool _isMetricsOverlayVisible = true;
    public bool IsMetricsOverlayVisible
    {
        get => _isMetricsOverlayVisible;
        set => this.RaiseAndSetIfChanged(ref _isMetricsOverlayVisible, value);
    }

    public MainWindowViewModel() : this(null!, null)
    {
    }

    public MainWindowViewModel(IDataProvider dataProvider, FrameMetrics? frameMetrics)
    {
        StartCommand = ReactiveCommand.CreateFromTask(StartDataProviderAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopDataProviderAsync);
        this.dataProvider = dataProvider;
        _frameMetrics = frameMetrics;
    }

    public async Task StartDataProviderAsync()
    {
        try
        {
            var tmp = SDRConstants.SignalPowerMax - SDRConstants.SignalPowerStart;

            dataProvider.Start();
            _ = Task.Run(async () =>
            {
                await foreach (var frame in dataProvider.Reader.ReadAllAsync())
                {
                    for (int i = 0; i < frame.Length; i++)
                    {
                        frame[i].SignalPower = (frame[i].SignalPower - SDRConstants.SignalPowerStart) / tmp;
                    }

                    _frameMetrics?.RecordFrame();
                    var snapshot = _frameMetrics?.Snapshot ?? default;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SpectrumData = frame;
                        FrameVersion++;

                        CurrentFps = snapshot.CurrentFps;
                        AvgFrameTimeMs = snapshot.AvgFrameTimeMs;
                        MinFrameTimeMs = snapshot.MinFrameTimeMs;
                        MaxFrameTimeMs = snapshot.MaxFrameTimeMs;
                        FreezeCount = snapshot.FreezeCount;
                    });  
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting data provider: {ex.Message}");
        }
    }

    public async Task StopDataProviderAsync()
    {
        try
        {
            await dataProvider.StopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping data provider: {ex.Message}");
        }
    }
}


public record struct NormalizeSignalPoint(double Frequency, double SignalPower);