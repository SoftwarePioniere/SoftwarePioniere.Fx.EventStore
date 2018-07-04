using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.DomainModel;
using SoftwarePioniere.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public class DomainEventStoreTests  : EventStoreTestsBase
    {
        public DomainEventStoreTests(ITestOutputHelper output) : base(output)
        { 
            ServiceCollection
                .AddEventStoreConnection();

            ServiceCollection.AddOptions()
                .Configure<EventStoreOptions>(c => new TestConfiguration().ConfigurationRoot.Bind("EventStore", c));

            ServiceCollection.AddDomainEventStore();
        }

        [Fact]
        public override Task CheckAggregateExists()
        {
            return base.CheckAggregateExists();
        }

        [Fact]
        public override void LoadThrowsErrorIfAggregateWithIdNotFound()
        {
            base.LoadThrowsErrorIfAggregateWithIdNotFound();
        }

        [Fact]
        public override Task SaveAndLoadContainsAllEventsForAnAggregate()
        {
            return base.SaveAndLoadContainsAllEventsForAnAggregate();
        }


        [Fact]
        public override Task SaveThrowsErrorIfVersionsNotMatch()
        {
            return base.SaveThrowsErrorIfVersionsNotMatch();
        }

        [Fact]
        public override Task SavesEventsWithExpectedVersion()
        {
            return base.SavesEventsWithExpectedVersion();
        }
    }
}
