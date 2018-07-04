using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.Projections;
using SoftwarePioniere.Projections.Services.EventStore;

// ReSharper disable once CheckNamespace
namespace SoftwarePioniere.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddEventStoreProjectionServices(this IServiceCollection services)
        {
            return services
                    .AddProjectionServices()   
                    .AddTransient<IProjectorRegistry, EventStoreProjectorRegistry>();

        }

    }
}
