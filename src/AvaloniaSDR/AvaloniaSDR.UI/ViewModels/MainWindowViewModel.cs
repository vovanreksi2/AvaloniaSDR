using Avalonia;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Providers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AvaloniaSDR.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IDataProvider dataProvider;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    private Point[]? spectrumData;
    public Point[]? SpectrumData { get => spectrumData; set => this.RaiseAndSetIfChanged(ref spectrumData, value); }

    public MainWindowViewModel() : this(null!)
    {
    }

 
    public MainWindowViewModel(IDataProvider dataProvider)
    {
        StartCommand = ReactiveCommand.CreateFromTask(StartDataProviderAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopDataProviderAsync);
        this.dataProvider = dataProvider;
        this.dataProvider.DataGenerated += OnDataGenerated;
    }
 

    private void OnDataGenerated(IEnumerable<SignalDataPoint> data)
    {
        SpectrumData = [.. data.Select(x => new Point(x.Frequency, x.SignalPower))];
    }

    public async Task StartDataProviderAsync()
    {
        try
        {
            dataProvider.Start();
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
