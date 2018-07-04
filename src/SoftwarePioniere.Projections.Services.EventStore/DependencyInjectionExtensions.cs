using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.Projections;
using SoftwarePioniere.Projections.Services.EventStore;

// ReSharper disable once CheckNamespace
namespace SoftwarePioniere.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddEventStoreProjections(this IServiceCollection services)
        {
            return services
                    .AddTransient<IProjectorRegistry, EventStoreProjectorRegistry>();


        }

    }
}
