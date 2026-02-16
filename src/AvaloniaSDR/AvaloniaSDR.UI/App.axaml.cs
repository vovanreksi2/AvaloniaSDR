using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Generators;
using AvaloniaSDR.UI.Processing.Resampler;
using AvaloniaSDR.UI.Processing.SignalNormalizer;
using AvaloniaSDR.UI.ViewModels;
using AvaloniaSDR.UI.Views.Waterfall;
using AvaloniaSDR.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace AvaloniaSDR.UI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    /// <summary>
    /// Exposes the DI container for controls that cannot receive constructor injection (XAML-instantiated).
    /// Resolve services here inside <see cref="Avalonia.Controls.Control.OnAttachedToVisualTree"/>.
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("ServiceProvider accessed before OnFrameworkInitializationCompleted.");

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
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IWaterfallColorMapper, WaterfallColorMapper>();
        services.AddSingleton<IWaterfallRingBuffer, WaterfallRingBuffer>();
        
        ConfigureProcessing(services);

        services.AddDataProvider(builder => builder
            .WithNoise()
            .AddSignal(new TimeVaryingSignalDescriptor(100.0, 0.5,
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

    private void ConfigureProcessing(IServiceCollection services)
    {
        services.AddSingleton<ISignalNormalizer, SignalNormalizer>();
        services.AddKeyedSingleton<ISpectrumResampler, MaxHoldDownsampler>(SpectrumResamplerKeys.Down);
        services.AddKeyedSingleton<ISpectrumResampler, LinearUpsamplingResampler>(SpectrumResamplerKeys.Up);
        services.AddSingleton<ISpectrumResampler, AdaptiveSpectrumResampler>();
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