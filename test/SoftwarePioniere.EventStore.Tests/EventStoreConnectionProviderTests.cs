using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                   .Configure<EventStoreOptions>(c => Configurator.Instance.ConfigurationRoot.Bind("EventStore", c));

            if (config != null)
            {
                opt.PostConfigure<EventStoreOptions>(config);
            }

            return GetService<EventStoreConnectionProvider>();
        }

        [Fact]
        public void CanConnectToStoreWithOutSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = false);
            var con = provider.Connection.Value;
            var meta = con.GetStreamMetadataAsync("$all", provider.AdminCredentials).Result;
            meta.Stream.Should().Be("$all");         
        }

        [Fact]
        public void CanConnectWithSsl()
        {
            var provider = CreateProvider(c => c.UseSslCertificate = true);
            var con = provider.Connection.Value;
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


        }
    }
}
