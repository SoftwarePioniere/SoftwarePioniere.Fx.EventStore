using System;
using Microsoft.Extensions.Logging;
using IEventStoreLogger = EventStore.ClientAPI.ILogger;

namespace SoftwarePioniere.EventStore
{
    public class EventStoreLogger : IEventStoreLogger
    {
        private readonly ILogger _logger;

        public EventStoreLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Error(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            _logger.LogError(1, ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            _logger.LogInformation(1, ex, format, args);
        }

        public void Debug(string format, params object[] args)
        {
            _logger.LogDebug(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            _logger.LogDebug(1, ex, format, args);
        }
    }
}
