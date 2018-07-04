using System;
using EventStore.ClientAPI;
using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.EventStore;

// ReSharper disable once CheckNamespace
namespace SoftwarePioniere.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddEventStoreConnection(this IServiceCollection services) =>
            services.AddEventStoreConnection(_ => { });

        public static IServiceCollection AddEventStoreConnection(this IServiceCollection services, Action<EventStoreOptions> configureOptions, 
            Action<ConnectionSettingsBuilder> connectionSetup = null)
        {

            var opt = services.AddOptions()
                .Configure(configureOptions)
                ;

            if (connectionSetup != null)
            {
                opt.PostConfigure<EventStoreOptions>(options => options.ConnectionSetup = connectionSetup);
            }

            services
                .AddSingleton<EventStoreConnectionProvider>()
                .AddTransient<EventStoreSetup>()
                ;

            return services;
        }
    }
}
