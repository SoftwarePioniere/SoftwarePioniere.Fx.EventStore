using System;
using System.Collections.Generic;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SoftwarePioniere.EventStore.Tests
{
    public class TestLoggerSerilogFactory : ILoggerFactory
    {
        private readonly Dictionary<string, LogLevel> _logLevels = new Dictionary<string, LogLevel>();
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private readonly ITestOutputHelper _testOutputHelper;
        

        public TestLoggerSerilogFactory(ITestOutputHelper output, LoggerConfiguration serilogConfig)
        {
            _testOutputHelper = output;
            _serilogger = serilogConfig.CreateLogger();
        }

        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
        public IReadOnlyList<LogEntry> LogEntries => _logEntries;

        public int MaxLogEntries = 1000;
        private Logger _serilogger;

        internal void AddLogEntry(LogEntry logEntry)
        {
            if (_logEntries.Count >= MaxLogEntries)
                return;

            lock (_logEntries)
                _logEntries.Add(logEntry);

            if (!ShouldWriteToTestOutput)
                return;

            try
            {
                _testOutputHelper.WriteLine(logEntry.ToString(false));
                _serilogger.Debug(logEntry.Message);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger2(categoryName, this);
        }

        public void AddProvider(ILoggerProvider loggerProvider)
        {
        }

        public bool ShouldWriteToTestOutput { get; set; } = true;

        public bool IsEnabled(string category, LogLevel logLevel)
        {
            if (_logLevels.TryGetValue(category, out LogLevel categoryLevel))
                return logLevel >= categoryLevel;

            return logLevel >= MinimumLevel;
        }

        public void SetLogLevel(string category, LogLevel minLogLevel)
        {
            _logLevels[category] = minLogLevel;
        }

        public void SetLogLevel<T>(LogLevel minLogLevel)
        {
            SetLogLevel(TypeHelper.GetTypeDisplayName(typeof(T)), minLogLevel);
        }

        public void Dispose()
        {
        }
    }
}
