using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoftwarePioniere.DomainModel;
using SoftwarePioniere.DomainModel.Services.EventStore;
using SoftwarePioniere.EventStore;

// ReSharper disable once CheckNamespace
namespace SoftwarePioniere.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddEventStoreDomainServices(this IServiceCollection services)
        {
            services
                .AddDomainServices()
                .AddSingleton<IEventStore, DomainEventStore>()
                .AddTransient<IHostedService, EventStoreInitializerBackgroundService>()
                .AddSingleton<IEventStoreInitializer, EventStoreSecurityInitializer>()
                ;
            
            return services;
        }
    }
}
