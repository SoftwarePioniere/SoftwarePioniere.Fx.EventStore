﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.PersistentSubscriptions;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ILogger = Microsoft.Extensions.Logging.ILogger;
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace SoftwarePioniere.EventStore
{
    /// <summary>
    /// Verbindung zum Event Store
    /// </summary>
    public sealed class EventStoreConnectionProvider
    {
        private readonly ILogger _logger;

        public ProjectionsManager CreateProjectionsManager()
        {
            var manager = new ProjectionsManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(5));
            return manager;
        }

        public UsersManager CreateUsersManager()
        {
            var manager = new UsersManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(5));
            return manager;
        }

        public PersistentSubscriptionsManager CreatePersistentSubscriptionsManager()
        {
            var manager = new PersistentSubscriptionsManager(new EventStoreLogger(_logger), GetHttpIpEndpoint(),
                TimeSpan.FromSeconds(5));
            return manager;
        }

        public IEventStoreConnection CreateNewConnection(Action<ConnectionSettingsBuilder> setup = null)
        {
            _logger.LogTrace("Creating Connection");

            IEventStoreConnection con;
            var connectionSettingsBuilder = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying();

            setup?.Invoke(connectionSettingsBuilder);

            if (!Options.UseCluster)
            {
                var ipa = GetHostIp(Options.IpEndPoint);

                if (Options.UseSslCertificate)
                {
                    _logger.LogInformation("Connceting To GetEventStore: with SSL IP: {0}:{1} // User: {2}", Options.IpEndPoint, Options.ExtSecureTcpPort, Options.OpsUsername);

                    var url = $"tcp://{ipa.MapToIPv4()}:{Options.ExtSecureTcpPort}";
                    connectionSettingsBuilder.UseSslConnection(Options.SslTargetHost, Options.SslValidateServer);
                    var uri = new Uri(url);
                    con = EventStoreConnection.Create(connectionSettingsBuilder, uri);
                }
                else
                {
                    _logger.LogInformation("Connceting To GetEventStore: without SSL IP: {0}:{1} // User: {2}", Options.IpEndPoint, Options.TcpPort, Options.OpsUsername);

                    var url = $"tcp://{ipa.MapToIPv4()}:{Options.TcpPort}";
                    var uri = new Uri(url);
                    con = EventStoreConnection.Create(connectionSettingsBuilder, uri);
                }
            }
            else
            {
                if (Options.UseSslCertificate)
                {
                    //var ipa = GetHostIp(Options.IpEndPoint);
                    //   var url = $"tcp://{ipa.MapToIPv4()}:{Options.ExtSecureTcpPort}";
                    connectionSettingsBuilder.UseSslConnection(Options.SslTargetHost, Options.SslValidateServer);
                }

                _logger.LogInformation("Connceting To GetEventStore: for Cluster // User: {0}", Options.OpsUsername);
                con = CreateForCluster(connectionSettingsBuilder);
            }

            RegisterEvents(con);
            //con.ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            return con;
        }

        public EventStoreConnectionProvider(ILoggerFactory loggerFactory, IOptions<EventStoreOptions> ioptions)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            if (ioptions == null)
                throw new ArgumentNullException(nameof(ioptions));

            var options = ioptions.Value;

            _logger = loggerFactory.CreateLogger(GetType());
            Options = options ?? throw new ArgumentNullException(nameof(options));

            OpsCredentials = new UserCredentials(options.OpsUsername, options.OpsPassword);
            AdminCredentials = new UserCredentials(options.AdminUsername, options.AdminPassword);

            _connection = new Lazy<IEventStoreConnection>(() => CreateNewConnection(options.ConnectionSetup));
        }

        private IEventStoreConnection CreateForCluster(ConnectionSettingsBuilder connectionSettingsBuilder)
        {
            var endpoints = Options.ClusterIpEndpoints.Select((x, i) =>
            {
                var ipa = GetHostIp(x);
                var port = Options.TcpPort;

                if (Options.ClusterHttpPorts.Length >= i + 1)
                {
                    port = Options.ClusterHttpPorts[i];
                }

                _logger.LogTrace($"Creating Cluster IP Endpoint: {ipa}:{port}");
                return new IPEndPoint(ipa, port);

            });

            var clusterSettings = ClusterSettings.Create()
                    .DiscoverClusterViaGossipSeeds()
                    .SetGossipTimeout(TimeSpan.FromMilliseconds(500))
                    .SetGossipSeedEndPoints(endpoints.ToArray())
                ;

            var con = EventStoreConnection.Create(connectionSettingsBuilder, clusterSettings);
            return con;
        }

        public EventHandler ConnectionChanged { get; set; }

        public EventHandler ConfigurationStateChanged { get; set; }

        private void OnConnectionChanged()
        {
            var handler = ConnectionChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void OnConfigurationStateChanged()
        {
            var handler = ConfigurationStateChanged;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private void RegisterEvents(IEventStoreConnection con)
        {
            con.Disconnected += (s, e) =>
            {
                _logger.LogInformation("EventStore Disconnected: {ConnectionName}", e.Connection.ConnectionName);
                IsConnected = false;
                OnConnectionChanged();

            };

            con.Reconnecting += (s, e) =>
            {
                _logger.LogInformation("EventStore Reconnecting: {ConnectionName}", e.Connection.ConnectionName);
            };

            con.Connected += (s, e) =>
            {
                _logger.LogInformation("EventStore Connected: {ConnectionName}", e.Connection.ConnectionName);
                IsConnected = true;


                //                if (!_isConfigured)
                //                {
                //                    _isConfigured = true;
                //                    _logger.LogInformation("EventStore Connected: {ConnectionName} - Starting Configuration", e.Connection.ConnectionName);
                //                    _configuration.ConfigureEventStore(this);
                //                }

                OnConnectionChanged();
            };
        }

        public void SetConfigurationState(bool isConfigured)
        {
            IsConfigured = isConfigured;
            OnConfigurationStateChanged();
        }


        public bool IsConnected { get; private set; }

        public bool IsConfigured { get; private set; }

        private readonly IDictionary<string, IPAddress> _hostIpAddresses = new Dictionary<string, IPAddress>();

        private IPAddress GetHostIp(string ipEndpoint)
        {
            _logger.LogTrace("GetHostIp for IpEndPoint {IpEndPoint}", ipEndpoint);

            if (_hostIpAddresses.ContainsKey(ipEndpoint))
                return _hostIpAddresses[ipEndpoint];

            if (!IPAddress.TryParse(ipEndpoint, out var ipa))
            {
                _logger.LogTrace("TryParse IP faulted, Try to lookup DNS");

                var hostIp = Dns.GetHostAddressesAsync(ipEndpoint).ConfigureAwait(false).GetAwaiter().GetResult();
                _logger.LogTrace($"Loaded {hostIp.Length} Host Addresses");
                foreach (var ipAddress in hostIp)
                {
                    _logger.LogTrace($"HostIp {ipAddress}");
                }

                if (hostIp.Length > 0)
                {

                    var hostIpAdress = hostIp.Last();
                    _hostIpAddresses.Add(ipEndpoint, hostIpAdress);
                    return hostIpAdress;
                }

                throw new InvalidOperationException("cannot resolve eventstore ip");
            }

            _hostIpAddresses.Add(ipEndpoint, ipa);
            return ipa;

        }

        private IPEndPoint _httpEndpoint;

        public IPEndPoint GetHttpIpEndpoint()
        {
            _logger.LogTrace("GetHttpIpEndpoint for IpEndPoint {IpEndPoint}", Options.IpEndPoint);

            if (_httpEndpoint != null)
                return _httpEndpoint;

            var ipa = GetHostIp(Options.IpEndPoint);

            _httpEndpoint = new IPEndPoint(ipa, Options.HttpPort);
            return _httpEndpoint;
        }

        /// <summary>
        /// Einstellungen
        /// </summary>
        public EventStoreOptions Options { get; }

        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        public async Task<IEventStoreConnection> GetActiveConnection()
        {
            //https://blog.cdemi.io/async-waiting-inside-c-sharp-locks/
            await SemaphoreSlim.WaitAsync();

            try
            {
                if (!_connection.IsValueCreated)
                {
                    var con = _connection.Value;
                    await con.ConnectAsync();
                    return con;
                }
            }
            finally
            {
                //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
                //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
                SemaphoreSlim.Release();
            }


            return _connection.Value;
        }

        private readonly Lazy<IEventStoreConnection> _connection;

        ///// <summary>
        ///// Connection als Lazy Objekt
        ///// </summary>
        //public Lazy<IEventStoreConnection> Connection { get; }

        /// <summary>
        /// Ops Verbindungsdaten
        /// </summary>
        public UserCredentials OpsCredentials { get; }

        /// <summary>
        /// Admin Verbindungsdaten
        /// </summary>
        public UserCredentials AdminCredentials { get; }


        public async Task<bool> IsStreamEmptyAsync(string streamName)
        {
            _logger.LogTrace("IsStreamEmptyAsync {StreamName}", streamName);

            //EventReadResult firstEvent = null;
            //try
            //{
            //    var conn = _provider.Connection.Value.Value;

            //    _logger.LogDebug("Try to read first event for stream to check if not empty:", streamName);
            //    //prüfen, ob es in dem stream ein event gibt. sonst ein dummy event eintragen
            //    firstEvent = conn.ReadEventAsync(streamName, 0, false, _provider.AdminCredentials).ConfigureAwait(false).GetAwaiter().GetResult();

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogDebug("cannot read first event");
            //    _logger.LogError(11, ex, ex.Message);
            //}

            //return Task.FromResult( firstEvent == null);

            var ret = false;

            //try
            //{
            var con = await GetActiveConnection();

            var slice = await con.ReadStreamEventsForwardAsync(streamName, 0, 1, false, AdminCredentials).ConfigureAwait(false);
            _logger.LogTrace("StreamExists {StreamName} : SliceStatus: {SliceStatus}", streamName, slice.Status);

            if (slice.Status == SliceReadStatus.StreamNotFound)
            {
                ret = true;
            }

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogDebug("cannot read first event");
            //    _logger.LogError(11, ex, ex.Message);

            //    ret = true;
            //}

            _logger.LogTrace("IsStreamEmptyAsync {StreamName} {IsEmpty}", streamName, ret);

            return ret;


        }

    }
}
