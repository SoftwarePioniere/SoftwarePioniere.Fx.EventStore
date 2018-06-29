using System;
using Foundatio.Logging.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public abstract class TestBase: TestWithLoggingBase
    {
        private IServiceProvider _serviceProvider;

        protected TestBase(ITestOutputHelper output) : base(output)
        {            
            ServiceCollection = new ServiceCollection()              
                .AddOptions()
                .AddSingleton<ILoggerFactory>(Log);

            Log.MinimumLevel = LogLevel.Trace;
        }


        protected IServiceCollection ServiceCollection { get; }

        private IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                    _serviceProvider = ServiceCollection.BuildServiceProvider();

                return _serviceProvider;
            }
        }


        protected T GetService<T>()
        {
            return ServiceProvider.GetRequiredService<T>();
        }

    }
}
