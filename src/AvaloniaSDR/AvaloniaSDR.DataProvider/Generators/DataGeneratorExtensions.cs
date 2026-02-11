using AvaloniaSDR.DataProvider.Providers;
using AvaloniaSDR.DataProvider.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSDR.DataProvider;

public static class DataGeneratorExtensions
{
    extension(IServiceCollection source)
    {
        public IServiceCollection AddDataProvider()
        {
            source.AddSingleton<IDataGenerator, OneSignalDataGenerator>();

            source.AddSingleton<IDataProvider, OneSignalDataProvider>();

            return source;
        }
    }
}
