using System;
using Microsoft.Extensions.Hosting;
using SoftwarePioniere.DomainModel.Subscriptions;
using SoftwarePioniere.EventStore;

namespace SoftwarePioniere.DomainModel.Services.EventStore.Subscriptions
{
    public class PersistentSubscriptionFactory : IPersistentSubscriptionFactory
    {
        private readonly EventStoreConnectionProvider _connectionProvider;
        private readonly IApplicationLifetime _applicationLifetime;
      

        public PersistentSubscriptionFactory(EventStoreConnectionProvider connectionProvider, IApplicationLifetime applicationLifetime)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        }


        public IPersistentSubscriptionAdapter<T> CreateAdapter<T>()
        {
            return new PersistentSubscriptionAdapter<T>(_connectionProvider, _applicationLifetime);
        }
    }
}