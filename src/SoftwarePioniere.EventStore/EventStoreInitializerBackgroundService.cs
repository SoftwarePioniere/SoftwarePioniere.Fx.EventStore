using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SoftwarePioniere.EventStore
{
    public class EventStoreInitializerBackgroundService : BackgroundService
    {
        private readonly IEnumerable<IEventStoreInitializer> _eventStoreInitializers;
        private readonly ILogger _logger;

        public EventStoreInitializerBackgroundService(ILoggerFactory loggerFactory, IEnumerable<IEventStoreInitializer> eventStoreInitializers)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger(GetType());
            _eventStoreInitializers = eventStoreInitializers;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("ExecuteAsync");

            var done = new List<Type>();

            foreach (var initializer in _eventStoreInitializers.OrderBy(x => x.ExecutionOrder))
            {
                if (!done.Contains(initializer.GetType()))
                {

                    _logger.LogInformation("Initialize IEventStoreInitializer {EventStoreInitializer}",
                        initializer.GetType().Name);
                    await initializer.InitializeAsync(stoppingToken);

                    done.Add(initializer.GetType());
                }
                else
                {
                    _logger.LogDebug("IEventStoreInitializer {EventStoreInitializer} already processed", initializer.GetType().Name);
                }
            }

            _logger.LogInformation("EventStore Initializer Finished");
        }
    }
}
