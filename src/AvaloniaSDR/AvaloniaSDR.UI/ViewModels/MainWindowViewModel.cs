using Avalonia.Threading;
using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Providers;
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


    public MainWindowViewModel() : this(null!)
    {
    }

 
    public MainWindowViewModel(IDataProvider dataProvider)
    {
        StartCommand = ReactiveCommand.CreateFromTask(StartDataProviderAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopDataProviderAsync);
        this.dataProvider = dataProvider;
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
                    await  Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SpectrumData = frame;
                        FrameVersion++;
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