using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.UserManagement;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.Messaging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SoftwarePioniere.EventStore
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class EventStoreSetup : IEventStoreSetup
    {
        private readonly ILogger _logger;
        private readonly EventStoreConnectionProvider _provider;

        public EventStoreSetup(EventStoreConnectionProvider provider, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger(GetType());
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task AddOpsUserToAdminsAsync()
        {
            _logger.LogTrace("AddOpsUserToAdminsAsync");

            if (await CheckOpsUserIsInAdminGroupAsync().ConfigureAwait(false))
            {
                _logger.LogTrace("ops user is already admin");
                return;
            }

            var manager = _provider.CreateUsersManager();

            var ops = await manager.GetUserAsync("ops", _provider.AdminCredentials).ConfigureAwait(false);
            if (ops.Groups == null || ops.Groups != null && !ops.Groups.Contains("$admins"))
            {
                var groups = new List<string>();
                if (ops.Groups != null)
                {
                    groups.AddRange(ops.Groups);
                }

                groups.Add("$admins");
                await manager.UpdateUserAsync("ops", ops.FullName, groups.ToArray(), _provider.AdminCredentials)
                    .ConfigureAwait(false);

                _logger.LogTrace("Group $admin added to ops user");
            }
        }

        public async Task<bool> CheckContinousProjectionIsCreatedAsync(string name, string query)
        {
            _logger.LogTrace("CheckContinousProjectionAsync: {ProjectionName}", name);

            var manager = _provider.CreateProjectionsManager();

            var list = await manager.ListContinuousAsync(_provider.AdminCredentials).ConfigureAwait(false);
            var proj = list.FirstOrDefault(x => x.Name == name);

            if (proj != null)
            {
                _logger.LogTrace("Projection found, compare");
                var existingQuery = await manager.GetQueryAsync(name, _provider.AdminCredentials).ConfigureAwait(false);
                if (EqualsCleanStrings(existingQuery, query))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> CheckOpsUserIsInAdminGroupAsync()
        {
            _logger.LogTrace("CheckOpsUserIsInAdminGroupAsync");

            var manager = new UsersManager(new EventStoreLogger(_logger),
                _provider.GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(5));

            var ops = await manager.GetUserAsync("ops", _provider.AdminCredentials).ConfigureAwait(false);

            if (ops.Groups == null || ops.Groups != null && !ops.Groups.Contains("$admins"))
            {
                _logger.LogTrace("Ops User not in admins");
                return false;
            }

            _logger.LogTrace("Ops User in admins");
            return true;
        }

        public async Task<bool> CheckProjectionIsRunningAsync(string name)
        {
            _logger.LogTrace("CheckProjectionIsRunningAsync: {ProjectionName}", name);

            var manager = _provider.CreateProjectionsManager();

            var list = await manager.ListContinuousAsync(_provider.AdminCredentials).ConfigureAwait(false);

            var proj = list.FirstOrDefault(x => x.Name == name);
            if (proj == null)
            {
                throw new InvalidOperationException($"Projection {name} not found. Please create it");
            }

            if (proj.Status.Equals("Running", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogTrace("Projection {ProjectionName} Running", name);
                return true;
            }

            _logger.LogDebug("Projection {ProjectionName} Not Running {Status}", name, proj.Status);
            return false;
        }

        public async Task CreateContinousProjectionAsync(string name, string query, bool trackEmittedStreams = false,
            bool? emitEnabled = false)
        {
            _logger.LogTrace("CreateContinousProjectionAsync: {ProjectionName}", name);

            var exists = await CheckContinousProjectionIsCreatedAsync(name, query).ConfigureAwait(false);

            if (exists)
            {
                _logger.LogTrace("ContinousProjection: {ProjectionName} already exist", name);
                return;
            }

            var manager = _provider.CreateProjectionsManager();

            var list = await manager.ListContinuousAsync(_provider.AdminCredentials).ConfigureAwait(false);

            var proj = list.FirstOrDefault(x => x.Name == name);
            if (proj != null)
            {
                _logger.LogDebug("Projection Query different Found: {ProjectionName}. Try Disable and Update", name);
                await manager.DisableAsync(name, _provider.AdminCredentials).ConfigureAwait(false);
                await manager.UpdateQueryAsync(name, query, emitEnabled, _provider.AdminCredentials);
                await manager.EnableAsync(name, _provider.AdminCredentials);
                await Task.Delay(1000).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Projection Not Found: {ProjectionName}. Try Create", name);
                await manager.CreateContinuousAsync(name, query, trackEmittedStreams, _provider.AdminCredentials)
                    .ConfigureAwait(false);
                await manager.UpdateQueryAsync(name, query, emitEnabled, _provider.AdminCredentials);
                await manager.EnableAsync(name, _provider.AdminCredentials);
                await Task.Delay(1000).ConfigureAwait(false);
            }

            exists = await CheckContinousProjectionIsCreatedAsync(name, query).ConfigureAwait(false);

            _logger.LogDebug("Projection: {ProjectionName}. Created. {exists}", name, exists);
        }

        public async Task CreatePersistentSubscriptionAsync(string stream, string group)
        {
            _logger.LogDebug("CreatePersistentSubscriptionAsync {Stream} {Group}", stream, group);

            var manager = _provider.CreatePersistentSubscriptionsManager();
            var cred = _provider.AdminCredentials;
            var con = _provider.Connection.Value;

            var list = await manager.List(cred);
            var exists = list.Any(x => string.Equals(x.EventStreamId, stream) &&
                                       string.Equals(x.GroupName, group));

            if (!exists)
            {
                _logger.LogInformation("Creating PersistentSubscription {Group} on {Stream}", group, stream);

                PersistentSubscriptionSettings settings = PersistentSubscriptionSettings.Create()
                    .DoNotResolveLinkTos()
                    .StartFromBeginning();

                //   configure?.Invoke(settings);

                await con.CreatePersistentSubscriptionAsync(stream, group, settings, cred);
            }
            else
            {
                _logger.LogDebug("PersistensSubscription {Group} on {Stream} exists", group, stream);
            }
        }

        public async Task DisableProjectionAsync(string name)
        {
            var isRunning = await CheckProjectionIsRunningAsync(name).ConfigureAwait(false);
            if (isRunning)
            {
                var manager = new ProjectionsManager(new EventStoreLogger(_logger),
                    _provider.GetHttpIpEndpoint(),
                    TimeSpan.FromSeconds(5));

                _logger.LogTrace("Projection running: {ProjectionName}. Try Disabling", name);
                await manager.DisableAsync(name, _provider.AdminCredentials).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Projection {ProjectionName} is not Running", name);
            }
        }

        public async Task EnableProjectionAsync(string name)
        {
            _logger.LogTrace("EnableProjectionAsync: {ProjectionName}", name);

            var isRunning = await CheckProjectionIsRunningAsync(name).ConfigureAwait(false);
            if (isRunning)
            {
                _logger.LogTrace("Projection {ProjectionName} already Running", name);
                return;
            }

            var manager = new ProjectionsManager(new EventStoreLogger(_logger),
                _provider.GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(5));

            _logger.LogTrace("Projection Not running: {ProjectionName}. Try Enabling", name);
            await manager.EnableAsync(name, _provider.AdminCredentials).ConfigureAwait(false);

            isRunning = await CheckProjectionIsRunningAsync(name).ConfigureAwait(false);
            var i = 1;
            while (i < 10 && !isRunning)
            {
                _logger.LogTrace("Waiting for Projection {ProjectionName} enabled.", name);
                await Task.Delay(2000).ConfigureAwait(false);
                i++;
                isRunning = await CheckProjectionIsRunningAsync(name).ConfigureAwait(false);
            }

            isRunning = await CheckProjectionIsRunningAsync(name).ConfigureAwait(false);
            _logger.LogDebug("Projection {ProjectionName} running : {running}", name, isRunning);
        }

        private static string CleanString(string dirty)
        {
            var clean = dirty.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
            return clean;
        }

        private static bool EqualsCleanStrings(string value1, string value2)
        {
            var clean1 = CleanString(value1);
            var clean2 = CleanString(value2);

            var eq = string.Equals(clean1, clean2, StringComparison.OrdinalIgnoreCase);

            return eq;
        }
    }
}