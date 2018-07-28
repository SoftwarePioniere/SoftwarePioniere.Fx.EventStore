using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.EventStore;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public class ProjectionBackgroundService : BackgroundService
    {
        private readonly IEnumerable<IEventStoreInitializer> _eventStoreInitializers;
        private readonly IProjectorRegistry _projectorRegistry;
        private readonly ILogger _logger;

        public ProjectionBackgroundService(ILoggerFactory loggerFactory, IEnumerable<IEventStoreInitializer> eventStoreInitializers, IProjectorRegistry projectorRegistry)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _eventStoreInitializers = eventStoreInitializers ?? throw new ArgumentNullException(nameof(eventStoreInitializers));
            _projectorRegistry = projectorRegistry ?? throw new ArgumentNullException(nameof(projectorRegistry));
            _logger = loggerFactory.CreateLogger(GetType());

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogDebug("ExecuteAsync");

            foreach (var initializer in _eventStoreInitializers.OrderBy(x=>x.ExecutionOrder))
            {
                _logger.LogDebug("InitializeAsync IEventStoreInitializer {EventStoreInitializer}",
                    initializer.GetType().Name);
                await initializer.InitializeAsync(stoppingToken);
            }

            _logger.LogDebug("IProjectorRegistry InitializeAsync");
            await _projectorRegistry.InitializeAsync(stoppingToken);
            
        }
    }
}
