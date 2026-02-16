using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.UI.Diagnostics;
using AvaloniaSDR.UI.ViewModels;
using AvaloniaSDR.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace AvaloniaSDR.UI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log((Exception)e.ExceptionObject);
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            mainWindow.DataContext = mainWindowViewModel;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<FrameMetrics>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddDataProvider(builder => builder
            .WithNoise()
            .AddSignal(new TimeVaryingSignalDescriptor(100.0, 0.1,
            [
                new SignalSegment(TimeSpan.FromSeconds(5), Power: 60.0),
                 new SignalSegment(TimeSpan.FromSeconds(3), Power: 20.0),
                 new SignalSegment(TimeSpan.FromSeconds(5), Power: 60.0),
            ], Loop: true))
            .AddSignal(new TimeVaryingSignalDescriptor(95.0, 0.5,
                [
                    new SignalSegment(TimeSpan.FromSeconds(1), Power: 61.0),
                     new SignalSegment(TimeSpan.FromSeconds(2), Power: 50.0),
                     new SignalSegment(TimeSpan.FromSeconds(3), Power: 60.0),
                     new SignalSegment(TimeSpan.FromSeconds(3), Power: 40.0),
                ], Loop: true))
            .AddSignal(new TimeVaryingSignalDescriptor(106, 1,
                [
                    new SignalSegment(TimeSpan.FromSeconds(1), Power: 61.0),
                     new SignalSegment(TimeSpan.FromSeconds(2), Power: 50.0),
                     new SignalSegment(TimeSpan.FromSeconds(3), Power: 60.0),
                     new SignalSegment(TimeSpan.FromSeconds(3), Power: 40.0),
                     new SignalSegment(TimeSpan.FromSeconds(5), Power: 0.0),
                ], Loop: true))
            );
    }


    private void Log(Exception exception)
    {
        if (_logger != null)
        {
            _logger.LogError(exception, "An unhandled exception occurred");
        }
        else
        {
            Console.WriteLine($"Error: {exception}");
        }
    }
}