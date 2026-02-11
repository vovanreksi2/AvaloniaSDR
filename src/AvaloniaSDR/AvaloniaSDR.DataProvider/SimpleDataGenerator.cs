using AvaloniaSDR.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSDR.DataProvider;

public class SimpleDataGenerator : IDataGenerator
{
    private readonly Random random;

    public SimpleDataGenerator()
    {
        random = new Random();    
    }

    public IEnumerable<SignalDataPoint> GenerateData()
    {
        var data = new SignalDataPoint[SDRConstants.Points];

        //TODO: Add noise
        for (int i = 0; i < SDRConstants.Points; i++)
        {
            var frequency = random.Next(SDRConstants.FrequencyStart, SDRConstants.FrequencyEnd + 1);
            var signalPower = random.Next(SDRConstants.SignalPowerStart, SDRConstants.SignalPowerMax + 1);

            data[i] = new SignalDataPoint(frequency, signalPower);
            yield return data[i];
        }
    }
}


public static class DataGeneratorExtensions
{
    extension(IServiceCollection source)
    {
        public IServiceCollection AddDataGenerator()
        {
            return source.AddSingleton<IDataGenerator, SimpleDataGenerator>();
        }
    }
}
