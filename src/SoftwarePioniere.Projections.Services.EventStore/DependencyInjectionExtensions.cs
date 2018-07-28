using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoftwarePioniere.EventStore;
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
                    .AddSingleton<IEventStoreInitializer, EventStoreSecurityInitializer>()
                    .AddSingleton<IEventStoreInitializer, EventStoreProjectionByCategoryInitializer>()
                    .AddTransient<IHostedService, ProjectionBackgroundService>()
                    .AddTransient<IProjectorRegistry, EventStoreProjectorRegistry>();

        }

    }
}
