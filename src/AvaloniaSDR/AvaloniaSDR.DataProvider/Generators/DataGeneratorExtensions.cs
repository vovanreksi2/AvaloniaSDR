using AvaloniaSDR.DataProvider.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSDR.DataProvider.Generators;

/// <summary>
/// Fluent builder for composing data generators before DI registration.
/// </summary>
public sealed class DataProviderBuilder
{
    private readonly List<TimeVaryingSignalDescriptor> _signals = [];
    private bool _includeNoise = true;

    /// <summary>Includes or excludes the noise floor generator (default: true).</summary>
    public DataProviderBuilder WithNoise(bool include = true)
    {
        _includeNoise = include;
        return this;
    }

    /// <summary>Adds a time-varying Gaussian signal to the composition.</summary>
    public DataProviderBuilder AddSignal(TimeVaryingSignalDescriptor descriptor)
    {
        _signals.Add(descriptor);
        return this;
    }

    /// <summary>
    /// Convenience overload: adds a constant-power signal with a single infinite segment.
    /// Backward-compat bridge for callers that previously used <see cref="SignalDescriptor"/>.
    /// </summary>
    public DataProviderBuilder AddSignal(double centerFreqMHz, double power, double widthMHz)
    {
        _signals.Add(new TimeVaryingSignalDescriptor(
            centerFreqMHz,
            widthMHz,
            [new SignalSegment(TimeSpan.MaxValue, power)],
            Loop: false));
        return this;
    }

    internal CompositeDataGenerator Build()
    {
        var generators = new List<IDataGenerator>();

        if (_includeNoise)
            generators.Add(new NoiseDataGenerator());

        var maxDuration = TimeSpan.Zero;

        foreach (var descriptor in _signals)
        {
            generators.Add(new SignalDataGenerator(descriptor));

            if (descriptor.Loop)
            {
                maxDuration = TimeSpan.MaxValue;
            }
            else
            {
                var d = descriptor.TotalDuration;
                if (d == TimeSpan.MaxValue)
                    maxDuration = TimeSpan.MaxValue;
                else if (maxDuration != TimeSpan.MaxValue && d > maxDuration)
                    maxDuration = d;
            }
        }

        // No finite signals registered → run indefinitely
        if (maxDuration == TimeSpan.Zero)
            maxDuration = TimeSpan.MaxValue;

        return new CompositeDataGenerator(generators, maxDuration);
    }
}

public static class DataGeneratorExtensions
{
    extension(IServiceCollection source)
    {
        /// <summary>
        /// Primary API: fluent builder for time-varying signal composition.
        /// </summary>
        /// <example>
        /// <code>
        /// services.AddDataProvider(builder => builder
        ///     .WithNoise()
        ///     .AddSignal(new TimeVaryingSignalDescriptor(100.0, 0.1,
        ///     [
        ///         new SignalSegment(TimeSpan.FromSeconds(5), Power: 60.0),
        ///         new SignalSegment(TimeSpan.FromSeconds(3), Power: 20.0),
        ///         new SignalSegment(TimeSpan.FromSeconds(5), Power: 60.0),
        ///     ], Loop: false)));
        /// </code>
        /// </example>
        public IServiceCollection AddDataProvider(Action<DataProviderBuilder> configure)
        {
            var builder = new DataProviderBuilder();
            configure(builder);
            var composite = builder.Build();

            source.AddSingleton<IDataGenerator>(composite);
            source.AddSingleton<IDataProvider, SignalDataProvider>();
            return source;
        }
    }
}
