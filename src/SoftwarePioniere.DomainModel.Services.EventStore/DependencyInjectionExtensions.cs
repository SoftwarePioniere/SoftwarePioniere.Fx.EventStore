using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.DomainModel;
using SoftwarePioniere.DomainModel.Services.EventStore;

// ReSharper disable once CheckNamespace
namespace SoftwarePioniere.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddDomainEventStore(this IServiceCollection services)
        {
            services
                .AddSingleton<IEventStore, DomainEventStore>()
                .AddSingleton<IEventStoreInitializer, EventStoreInitializer>()
                ;

            return services;
        }
    }
}
