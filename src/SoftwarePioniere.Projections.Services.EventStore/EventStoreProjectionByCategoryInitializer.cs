using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.EventStore;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public class EventStoreProjectionByCategoryInitializer : IEventStoreInitializer
    {
        private readonly EventStoreSetup _setup;

        private readonly ILogger _logger;

        public EventStoreProjectionByCategoryInitializer(ILoggerFactory loggerFactory
            , EventStoreSetup setup)
        {
            _setup = setup;

            _logger = loggerFactory.CreateLogger(GetType());
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogDebug("InitializeAsync");

            if (!await _setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                _logger.LogInformation("Enabling $by_category Projection");
                await _setup.EnableProjectionAsync("$by_category");
            }
        }

        public int ExecutionOrder { get; } = (int.MinValue + 1);
    }
}
