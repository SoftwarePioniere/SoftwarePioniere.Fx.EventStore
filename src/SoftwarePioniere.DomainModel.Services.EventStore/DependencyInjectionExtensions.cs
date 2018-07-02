using Microsoft.Extensions.DependencyInjection;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddDomainEventStore(this IServiceCollection services)
        {
            services.AddSingleton<IEventStore, DomainEventStore>();

            return services;
        }
    }
}
