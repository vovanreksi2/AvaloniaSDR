using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSDR.DataProvider;

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
