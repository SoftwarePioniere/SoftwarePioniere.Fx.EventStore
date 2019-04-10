using SoftwarePioniere.DomainModel;
using SoftwarePioniere.DomainModel.Services.EventStore;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddEventStoreDomainServices(this IServiceCollection services)
        {
            services
             //   .AddDomainServices()
                .AddSingleton<IEventStore, DomainEventStore>()           
                .AddSingleton<IProjectionReader, ProjectionReader>()
                ;
            
            return services;
        }
    }
}
