using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.Messaging;

namespace SoftwarePioniere.EventStore
{
    public class EventStoreSecurityInitializer : IEventStoreInitializer
    {
        private readonly IEventStoreSetup _setup;

        private readonly ILogger _logger;

        public EventStoreSecurityInitializer(ILoggerFactory loggerFactory
            , IEventStoreSetup setup)
        {
            _setup = setup;
            _logger = loggerFactory.CreateLogger(GetType());
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogTrace("InitializeAsync");

            if (!await _setup.CheckOpsUserIsInAdminGroupAsync())
            {
                _logger.LogDebug("Adding Opsuser to Admin Group");
                await _setup.AddOpsUserToAdminsAsync();
            }
        }

        public int ExecutionOrder { get; } = int.MinValue;
    }
}
