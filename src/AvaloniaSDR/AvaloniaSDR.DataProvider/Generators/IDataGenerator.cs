namespace AvaloniaSDR.DataProvider.Generators;

public interface IDataGenerator
{
    IEnumerable<SignalDataPoint> GenerateData();
}