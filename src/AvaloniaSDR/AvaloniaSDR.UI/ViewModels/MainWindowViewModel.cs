using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using AvaloniaSDR.DataProvider;
using System.Text;
using AvaloniaSDR.DataProvider.Providers;

namespace AvaloniaSDR.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IDataProvider dataProvider;

    public string Greeting { get; } = "Welcome to Avalonia!";

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => _text = value;
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand UpdateCommand { get; }

    public MainWindowViewModel() : this(null!)
    {
    }

    public MainWindowViewModel(IDataProvider dataProvider)
    {
        StartCommand = ReactiveCommand.CreateFromTask(StartDataProviderAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopDataProviderAsync);
        UpdateCommand = ReactiveCommand.Create(() => this.RaisePropertyChanged(nameof(Text)));

        this.dataProvider = dataProvider;
        dataProvider.DataGenerated += OnDataGenerated;
    }

    private void OnDataGenerated(IEnumerable<SignalDataPoint> data)
    {
        var sb = new StringBuilder();
        foreach (SignalDataPoint dataPoint in data) 
        {
            sb.AppendLine($"{dataPoint.Frequency},{dataPoint.SignalPower}");
        }
        Text += sb.ToString();
    }

    public async Task StartDataProviderAsync()
    {
        try
        {
            dataProvider.Start();
            Text = "Data provider started";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting data provider: {ex.Message}");
            Text = $"Error: {ex.Message}";
        }
    }

    public async Task StopDataProviderAsync()
    {
        try
        {
            await dataProvider.StopAsync();
            Text = "Data provider stopped";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping data provider: {ex.Message}");
            Text = $"Error: {ex.Message}";
        }
    }
}
