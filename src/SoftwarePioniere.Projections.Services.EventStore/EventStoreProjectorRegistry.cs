﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Foundatio.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.Messaging;
using SoftwarePioniere.ReadModel;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public class EventStoreProjectorRegistry : IProjectorRegistry
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly EventStoreConnectionProvider _connectionProvider;
        private readonly IEnumerable<IReadModelProjector> _projectors;
        private readonly IEntityStore _entityStore;
        private readonly ICacheClient _cache;
        private readonly ILogger _logger;

        public EventStoreProjectorRegistry(ILoggerFactory loggerFactory
            , EventStoreConnectionProvider connectionProvider
            , IServiceProvider serviceProvider
            , IEntityStore entityStore
            , ICacheClient cache
        )
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));

            _logger = loggerFactory.CreateLogger(GetType());
            _projectors = serviceProvider.GetServices<IReadModelProjector>();
            _entityStore = entityStore ?? throw new ArgumentNullException(nameof(entityStore));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }


        //private async Task<bool> ReadStreamAsync(string stream, IQueue<ProjectionEventData> queue, CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    _logger.LogDebug("ReadFromStreamAsync {Stream}", stream);

        //    var cred = _connectionProvider.OpsCredentials;
        //    var src = _connectionProvider.Connection.Value;

        //    StreamEventsSlice slice;

        //    long sliceStart = StreamPosition.Start;


        //    do
        //    {
        //        _logger.LogTrace("Reading Slice from {0}", sliceStart);

        //        slice = await src.ReadStreamEventsForwardAsync(stream, sliceStart, 500, true, cred);
        //        _logger.LogTrace("Next Event: {0} , IsEndOfStream: {1}", slice.NextEventNumber, slice.IsEndOfStream);

        //        sliceStart = slice.NextEventNumber;

        //        foreach (var ev in slice.Events)
        //        {
        //            if (cancellationToken.IsCancellationRequested)
        //            {
        //                _logger.LogWarning("Initialization Cancelled");
        //                return false;
        //            }

        //            var de = ev.Event.ToDomainEvent();
        //            var desc = new ProjectionEventData
        //            {
        //                EventData = de,
        //                EventNumber = ev.OriginalEventNumber
        //            };

        //            _logger.LogTrace("Enqueue Event @{0}", desc);
        //            await queue.EnqueueAsync(desc);

        //        }

        //    } while (!slice.IsEndOfStream);

        //    return true;
        //}


        private async Task<ProjectionStatus> ReadStreamAsync(string stream, EventStoreProjectionContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogDebug("ReadFromStreamAsync {Stream} {ProjectorId}", stream, context.ProjectorId);
            var sw = Stopwatch.StartNew();

            var cred = _connectionProvider.OpsCredentials;
            var src = await _connectionProvider.GetActiveConnection();

            StreamEventsSlice slice;

            long sliceStart = StreamPosition.Start;

            do
            {
                _logger.LogTrace("Reading Slice from {0}", sliceStart);

                slice = await src.ReadStreamEventsForwardAsync(stream, sliceStart, 500, true, cred);
                _logger.LogTrace("Next Event: {0} , IsEndOfStream: {1}", slice.NextEventNumber, slice.IsEndOfStream);

                sliceStart = slice.NextEventNumber;

                foreach (var ev in slice.Events)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Initialization Cancelled");
                        return null;
                    }

                    try
                    {
                        var de = ev.Event.ToDomainEvent();

                        if (de != null)
                        {
                            var entry = new ProjectionEventData
                            {
                                EventData = de,
                                EventNumber = ev.OriginalEventNumber
                            };

                            await context.HandleEventAsync(entry);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error Reading Event: {Stream} {ProjectorId} {OriginalEventNumber}", stream,
                            context.ProjectorId, ev.OriginalEventNumber);
                    }

                }

            } while (!slice.IsEndOfStream);

            _logger.LogDebug("ReadFromStreamAsync {Stream} {ProjectorId} Finished in {Elapsed:0.0000} ms", stream, context.ProjectorId, sw.ElapsedMilliseconds);

            return context.Status;

        }



        private async Task InsertEmptyDomainEventIfStreamIsEmpty(string streamName)
        {
            _logger.LogDebug("InsertEmptyDomainEventIfStreamIsEmpty {StreamName}", streamName);

            var empty = await _connectionProvider.IsStreamEmptyAsync(streamName);

            if (empty)
            {
                _logger.LogDebug("Stream is Empty {StreamName}", streamName);

                var events = new[] { new EmptyDomainEvent().ToEventData(null) };

                var name = streamName; // $"{aggregateName}-empty";

                //wenn es eine category stream ist, dann den basis stream finden und eine neue gruppe -empty erzeugen
                if (streamName.Contains("$ce-"))
                    name = $"{streamName.Replace("$ce-", string.Empty)}-empty";

                _logger.LogDebug("InsertEmptyEvent: StreamName {StreamName}", name);
                var con = await _connectionProvider.GetActiveConnection();
                await con.AppendToStreamAsync(name, -1, events, _connectionProvider.OpsCredentials).ConfigureAwait(false);

            }
            else
            {
                _logger.LogDebug("Stream is not Empty {StreamName}", streamName);
            }

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting InitializeAsync");
            var sw = Stopwatch.StartNew();

            foreach (var projector in _projectors)
            {
                if (string.IsNullOrEmpty(projector.StreamName))
                {
                    throw new InvalidOperationException($"Empty Stream in Projector: {projector.GetType().FullName}");
                }
            }
            var streamsToCheck = _projectors.Select(x => x.StreamName).Distinct().ToArray();
            foreach (var s in streamsToCheck)
            {
                _logger.LogDebug("Preparing Stream {StreamName}", s);
                await InsertEmptyDomainEventIfStreamIsEmpty(s);
            }

            foreach (var projector in _projectors)
            {
                if (projector != null)
                {
                    var projectorId = projector.GetType().FullName;
                    await _cache.RemoveByPrefixAsync(CacheKeys.Create<ProjectionInitializationStatus>());
                    var statusItem = await _entityStore.LoadAsync<ProjectionInitializationStatus>(projectorId, cancellationToken);

                    if (statusItem.IsNew)
                    {
                        var entity = statusItem.Entity;
                        entity.ProjectorId = projectorId;
                        entity.StreamName = projector.StreamName;
                        entity.Status = ProjectionInitializationStatus.StatusNew;
                        entity.ModifiedOnUtc = DateTime.UtcNow;
                        await _entityStore.SaveAsync(statusItem, cancellationToken);
                    }
                }
            }

            var tasks = new List<Task>();

            foreach (var projector in _projectors)
            {
                //await InitProjectorInternal(cancellationToken, projector);
                var t = InitProjectorInternal(cancellationToken, projector);
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation("EventStore Projection Initializer Finished in {Elapsed:0.0000} ms", sw.ElapsedMilliseconds);
        }

        private async Task InitProjectorInternal(CancellationToken cancellationToken, IReadModelProjector projector)
        {
            var projectorId = projector.GetType().FullName;
            projector.Initialize(cancellationToken);

            _logger.LogInformation("Initialize Projector {ProjectorName}", projectorId);

            await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusPending, "Starting");

            var context =
                new EventStoreProjectionContext(_loggerFactory, _connectionProvider, _entityStore, projector)
                {
                    StreamName = projector.StreamName,
                    ProjectorId = projectorId
                };

            projector.Context = context;

            await _cache.RemoveByPrefixAsync(CacheKeys.Create<ProjectionStatus>());
            var status = await _entityStore.LoadAsync<ProjectionStatus>(projectorId, cancellationToken);
            context.Status = status.Entity;

            if (status.IsNew)
            {
                _logger.LogDebug("Starting Empty Initialization for Projector {Projector}", projectorId);

                await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusPending, "StartingInitialization");

                await context.StartInitializationModeAsync();

                await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusPending, "InitializationStartingStreamReading");

                try
                {
                    var tempStatus = await ReadStreamAsync(context.StreamName, context, cancellationToken);

                    if (tempStatus != null)
                    {
                        status.Entity.LastCheckPoint = tempStatus.LastCheckPoint;
                        status.Entity.ModifiedOnUtc = tempStatus.ModifiedOnUtc;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ReadStreamAsync");
                    throw;
                }

                await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusPending, "InitializationStartingCopy");

                try
                {
                    await projector.CopyEntitiesAsync(context.EntityStore, _entityStore, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error CopyEntitites");
                    throw;
                }

                await context.StopInitializationModeAsync();
                await _entityStore.SaveAsync(status, cancellationToken);

                await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusPending, "InitializationFinished");

            }

            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Starting Subscription on EventStore for Projector {Projector}", context.ProjectorId);
            await context.StartSubscription(cancellationToken);
            await UpdateInitializationStatusAsync(cancellationToken, projectorId, ProjectionInitializationStatus.StatusReady, "Startet");
        }

        private async Task UpdateInitializationStatusAsync(CancellationToken cancellationToken, string projectorId, string status, string statusText)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _cache.RemoveByPrefixAsync(CacheKeys.Create<ProjectionInitializationStatus>());
            var statusItem = await _entityStore.LoadAsync<ProjectionInitializationStatus>(projectorId, cancellationToken);
            statusItem.Entity.Status = status;
            statusItem.Entity.StatusText = statusText;
            await _entityStore.SaveAsync(statusItem, cancellationToken);
        }


        public async Task<ProjectionRegistryStatus> GetStatusAsync()
        {
            _logger.LogTrace("GetStatusAsync");

            var states = await _entityStore.LoadItemsAsync<ProjectionInitializationStatus>();

            var temp = new ProjectionRegistryStatus()
            {
                Projectors = states,
                Status = ProjectionInitializationStatus.StatusNew
            };

            if (states.Length == 0)
            {
                return temp;
            }

            if (states.All(x => x.Status == ProjectionInitializationStatus.StatusReady))
            {
                temp.Status = ProjectionInitializationStatus.StatusReady;
            }
            else
            {
                if (states.Any(x => x.Status == ProjectionInitializationStatus.StatusPending))
                {
                    temp.Status = ProjectionInitializationStatus.StatusPending;
                }
                else
                {
                    temp.Status = ProjectionInitializationStatus.StatusNew;
                }
            }

            temp.Pending = temp.Projectors.Count(x => x.Status == ProjectionInitializationStatus.StatusPending);
            temp.Ready = temp.Projectors.Count(x => x.Status == ProjectionInitializationStatus.StatusReady);
            temp.Total = temp.Projectors.Length;

            return temp;
        }


    }
}
