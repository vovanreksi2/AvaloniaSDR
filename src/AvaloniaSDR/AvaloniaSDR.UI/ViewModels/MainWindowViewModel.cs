using Avalonia.Threading;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Providers;
using AvaloniaSDR.UI.Processing.SignalNormalizer;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AvaloniaSDR.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IDataProvider dataProvider;
    private readonly ISignalNormalizer normalizer;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    private SignalDataPoint[]? spectrumData;
    public SignalDataPoint[]? SpectrumData
    {
        get => spectrumData;
        set => this.RaiseAndSetIfChanged(ref spectrumData, value);
    }

    private SignalDataPoint[]? _lastFrame;

    private bool isRunning;
    public bool IsRunning
    {
        get => isRunning;
        set => this.RaiseAndSetIfChanged(ref isRunning, value);
    }

    public MainWindowViewModel() : this(null!, null!)     {    }

    public MainWindowViewModel(IDataProvider dataProvider, ISignalNormalizer normalizer)
    {
        StartCommand = ReactiveCommand.CreateFromTask(StartDataProviderAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopDataProviderAsync);
        this.dataProvider = dataProvider;
        this.normalizer = normalizer;
    }

    public SignalDataPoint[]? GetLastFrame() => _lastFrame;

    public async Task StartDataProviderAsync()
    {
        try
        {
            IsRunning = true;
            dataProvider.Start();
            _ = Task.Run(async () =>
            {
                await foreach (var frame in dataProvider.Reader.ReadAllAsync())
                {
                    normalizer.Normalize(frame);

                    _lastFrame = frame;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SpectrumData = frame;
                    }, DispatcherPriority.Background);
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
            IsRunning = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping data provider: {ex.Message}");
        }
    }
}
