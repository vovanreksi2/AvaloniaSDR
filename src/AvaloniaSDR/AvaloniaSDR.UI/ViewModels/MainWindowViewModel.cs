using System.Diagnostics;
using AvaloniaSDR.DataProvider;

namespace AvaloniaSDR.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";

    public MainWindowViewModel()
    {
        var dataGenerator = new SimpleDataGenerator();
        var data = dataGenerator.GenerateData();

        foreach (var point in data)
        {
            Debug.WriteLine($"Frequency: {point.Frequency} MHz, Signal Power: {point.SignalPower} dBm");
        }
    }
}
