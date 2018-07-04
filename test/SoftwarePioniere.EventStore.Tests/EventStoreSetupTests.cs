using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwarePioniere.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace SoftwarePioniere.EventStore.Tests
{
    public class EventStoreSetupTests : TestBase
    {

        private EventStoreSetup CreateSetup()
        {
    

            return GetService<EventStoreSetup>();          
        }

         [Fact]
        public async Task CanEnableProjectionTest()
        {
            var setup = CreateSetup();
            const string projectionName = "$streams";
            if (await setup.CheckProjectionIsRunningAsync(projectionName))
            {
                throw new InvalidOperationException($"Please disable Projection {projectionName} before testing");
            }

            await setup.EnableProjectionAsync(projectionName);
            (await setup.CheckProjectionIsRunningAsync(projectionName)).Should().BeTrue();
        }

        [Fact]
        public async Task CanAddOpsUserToAdminTest()
        {
            var setup = CreateSetup();
            if (await setup.CheckOpsUserIsInAdminGroupAsync())
            {
                throw new InvalidOperationException("Please remove ops user from admins");
            }

            await setup.AddOpsUserToAdminsAsync();
            (await setup.CheckOpsUserIsInAdminGroupAsync()).Should().BeTrue();
        }

        [Fact]
        public async Task CanCreateContinousProjectionTest()
        {
            var setup = CreateSetup();

            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("TestSubscription.js");

            if (await setup.CheckContinousProjectionIsCreatedAsync(name, query))
            {
                throw new InvalidOperationException($"Please remove projection {name}");
            }

            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();

            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();
        }

        [Fact]
        public async Task CanUpdateContinousProjectionTest()
        {
            var setup = CreateSetup();

            var name = $"tests{Guid.NewGuid().ToString().Replace("-", "")}";
            var query = TestFiles.GetFileContent("TestSubscription.js");

            if (await setup.CheckContinousProjectionIsCreatedAsync(name, query))
            {
                throw new InvalidOperationException($"Please remove projection {name}");
            }

            //erstes erstellen
            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();

            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();

            //neu laden und updaten
            query = TestFiles.GetFileContent("TestSubscription1.js");
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeFalse();

            await setup.CreateContinousProjectionAsync(name, query);
            (await setup.CheckContinousProjectionIsCreatedAsync(name, query)).Should().BeTrue();

            (await setup.CheckProjectionIsRunningAsync(name)).Should().BeTrue();
        }

        public EventStoreSetupTests(ITestOutputHelper output) : base(output)
        {
            ServiceCollection
                .AddEventStoreConnection();

                ServiceCollection
                    .AddOptions()
                    .Configure<EventStoreOptions>(c => new TestConfiguration().ConfigurationRoot.Bind("EventStore", c));
        }
    }
}
