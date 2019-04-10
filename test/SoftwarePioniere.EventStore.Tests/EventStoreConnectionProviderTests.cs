using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public class EventStoreConnectionProviderTests : TestBase
    {
        private EventStoreConnectionProvider CreateProvider(Action<EventStoreOptions> config = null)
        {
            ServiceCollection
                .AddEventStoreConnection();

            var opt = ServiceCollection.AddOptions()
                   .Configure<EventStoreOptions>(c => new TestConfiguration().ConfigurationRoot.Bind("EventStore", c));

            if (config != null)
            {
                opt.PostConfigure(config);
            }

            return GetService<EventStoreConnectionProvider>();
        }

        [Fact]
        public  async Task CanConnectToStoreWithOutSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = false);
            var con = await provider.GetActiveConnection();
            var meta = con.GetStreamMetadataAsync("$all", provider.AdminCredentials).Result;
            meta.Stream.Should().Be("$all");
        }

        [Fact]
        public async Task CanConnectWithSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = true);
            var con = await provider.GetActiveConnection();
            var meta = con.GetStreamMetadataAsync("$all", provider.AdminCredentials).Result;
            meta.Stream.Should().Be("$all");
        }

        [Fact]
        public async Task CanCheckIfStreamIsEmpty()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = false);

            var streamId = Guid.NewGuid().ToString().Replace("-", "");
            var empty = await provider.IsStreamEmptyAsync(streamId);
            empty.Should().BeTrue();
        }

        public EventStoreConnectionProviderTests(ITestOutputHelper output) : base(output)
        {
            
         
            var loggerConfiguration = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console()
//#if !DEBUG
                    .WriteTo.File("/testresults/log.txt")
//#endif

                ;
//           log.Debug("Test Loggy");

            var lf = new TestLoggerSerilogFactory(output, loggerConfiguration);
            ServiceCollection
                .AddSingleton<ILoggerFactory>(lf);
            
            //Log.AddSerilog(loggerConfiguration);

            //output.WriteLine("ctor");
        }
    }
}
