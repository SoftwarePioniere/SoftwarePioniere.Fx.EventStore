using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public class ProjectionBackgroundService : BackgroundService
    {
        private readonly IProjectorRegistry _projectorRegistry;
        private readonly ILogger _logger;

        public ProjectionBackgroundService(ILoggerFactory loggerFactory, IProjectorRegistry projectorRegistry)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectorRegistry = projectorRegistry ?? throw new ArgumentNullException(nameof(projectorRegistry));
            _logger = loggerFactory.CreateLogger(GetType());

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("ExecuteAsync");
          
            _logger.LogDebug("Initialize IProjectorRegistry");
            await _projectorRegistry.InitializeAsync(stoppingToken);

        }
    }
}
