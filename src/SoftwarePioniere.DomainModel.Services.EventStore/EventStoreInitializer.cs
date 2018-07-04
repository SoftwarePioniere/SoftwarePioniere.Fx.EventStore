using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.EventStore;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{
    public class EventStoreInitializer : IEventStoreInitializer
    {
        private readonly EventStoreSetup _setup;

        private readonly ILogger _logger;

        public EventStoreInitializer(ILoggerFactory loggerFactory
            , EventStoreSetup setup)
        {
            _setup = setup;

            _logger = loggerFactory.CreateLogger(GetType());
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("InitializeAsync");

            if (!await _setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                _logger.LogDebug("Enable $by_category Projection");
                await _setup.EnableProjectionAsync("$by_category");
            }

            if (!await _setup.CheckOpsUserIsInAdminGroupAsync())
            {
                _logger.LogDebug("Adding Opsuser to Admin Group");
                await _setup.AddOpsUserToAdminsAsync();
            }
        }
    }
}
