using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.DomainModel;
using SoftwarePioniere.DomainModel.FakeDomain;
using SoftwarePioniere.Extensions.DependencyInjection;
using SoftwarePioniere.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public class ProjectionReaderTests : TestBase
    {
       
        [Fact]
        public async Task ProjectionReaderTest()
        {
            var setup = GetService<EventStoreSetup>();
            var store = GetService<IEventStore>();
            var proj = GetService<IProjectionReader>();

            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("FakeCounterProjection.js");

            await setup.AddOpsUserToAdminsAsync();

            if (!await setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                await setup.EnableProjectionAsync("$by_category");
            }
            (await setup.CheckProjectionIsRunningAsync("$by_category")).Should().BeTrue();


            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();
            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();

            var save = FakeEvent.CreateList(155).ToArray();
            store.SaveEventsAsync<FakeAggregate>(save.First().AggregateId, save, 154).Wait();
            await Task.Delay(1500);

            var result = await proj.GetStateAsync<X1>(name, save.First().AggregateId.Replace("-", ""));

            result.Should().NotBeNull();
            result.Ids.Length.Should().Be(save.Length);
        }

        public class X1
        {
            public string[] Ids { get; set; }
        }

        [Fact]
        public async Task ProjectionReaderTest2()
        {
            var setup = GetService<EventStoreSetup>();
            var store = GetService<IEventStore>();
            var proj = GetService<IProjectionReader>();

            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("FakeCounterProjection.js");

            await setup.AddOpsUserToAdminsAsync();

            if (!await setup.CheckProjectionIsRunningAsync("$by_category"))
            {
                await setup.EnableProjectionAsync("$by_category");
            }
            (await setup.CheckProjectionIsRunningAsync("$by_category")).Should().BeTrue();


            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();
            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();

            var save = FakeEvent.CreateList(155).ToArray();
            store.SaveEventsAsync<FakeAggregate>(save.First().AggregateId, save, 154).Wait();

            await Task.Delay(1500);

            var definition = new
            {
                Ids = new string[0]
            };
            
            var result = await proj.GetStateAsyncAnonymousType(name, definition, save.First().AggregateId.Replace("-", ""));

            result.Should().NotBeNull();
            result.Ids.Length.Should().Be(save.Length);
        }


        public ProjectionReaderTests(ITestOutputHelper output) : base(output)
        {
            ServiceCollection
                .AddEventStoreConnection()
                .AddEventStoreDomainServices()
                ;

            ServiceCollection
                .AddOptions()
                .Configure<EventStoreOptions>(c => new TestConfiguration().ConfigurationRoot.Bind("EventStore", c));

        }
    }
}
