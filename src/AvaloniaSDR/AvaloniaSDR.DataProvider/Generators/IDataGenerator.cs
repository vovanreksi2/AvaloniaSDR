namespace AvaloniaSDR.DataProvider.Generators;

public interface IDataGenerator
{
    /// <summary>
    /// Produces a single frame of signal data (SDRConstants.Points elements).
    /// </summary>
    /// <param name="elapsed">Simulation time since the provider started.</param>
    SignalDataPoint[] GenerateData(TimeSpan elapsed);

    /// <summary>
    /// Total duration this generator produces meaningful output.
    /// <see cref="TimeSpan.MaxValue"/> means the generator runs indefinitely.
    /// </summary>
    TimeSpan TotalDuration { get; }
}