﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
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
        private readonly ILogger _logger;

        private readonly IDictionary<string, ProjectionInfo>
            _infos = new ConcurrentDictionary<string, ProjectionInfo>();

        public EventStoreProjectorRegistry(ILoggerFactory loggerFactory
            , EventStoreConnectionProvider connectionProvider
            , IEnumerable<IReadModelProjector> projectors
            , IEntityStore entityStore
        )
        {

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));

            _logger = loggerFactory.CreateLogger(GetType());
            _projectors = projectors ?? throw new ArgumentNullException(nameof(projectors));
            _entityStore = entityStore ?? throw new ArgumentNullException(nameof(entityStore));

            foreach (var projector in projectors)
            {
                if (projector != null)
                {

                    var id = projector.GetType().FullName;

                    var i = new ProjectionInfo
                    {
                        ProjectorId = id,
                        StreamName = projector.StreamName,
                        Status = "None"
                    };

                    if (id != null) _infos.Add(id, i);
                }
            }

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


        private async Task<bool> ReadStreamAsync(string stream, EventStoreProjectionContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogDebug("ReadFromStreamAsync {Stream} {ProjectorId}", stream, context.ProjectorId);

            var cred = _connectionProvider.OpsCredentials;
            var src = _connectionProvider.Connection.Value;

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
                        return false;
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

            return true;
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
                await _connectionProvider.Connection.Value
                    .AppendToStreamAsync(name, -1, events, _connectionProvider.OpsCredentials).ConfigureAwait(false);

            }
            else
            {
                _logger.LogDebug("Stream is not Empty {StreamName}", streamName);
            }

        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogDebug("InitializeAsync");

            var streamsToCheck = _projectors.Select(x => x.StreamName).Distinct().ToArray();
            foreach (var s in streamsToCheck)
            {
                _logger.LogInformation("Preparing Stream {StreamName}", s);
                await InsertEmptyDomainEventIfStreamIsEmpty(s);
            }




            foreach (var projector in _projectors)
            {
                var projectorId = projector.GetType().FullName;

                var info = _infos[projectorId ?? throw new InvalidOperationException()];
                info.Status = "New";

                _logger.LogInformation("Initialize Projector {ProjectorName}", projector.GetType().Name);

                //await InsertEmptyDomainEventIfStreamIsEmpty(projector.StreamName);

                var context =
                    new EventStoreProjectionContext(_loggerFactory, _connectionProvider, _entityStore, projector)
                    {
                        StreamName = projector.StreamName,
                        ProjectorId = projectorId
                    };

                projector.Context = context;

                //try load statuc
                var status = await _entityStore.LoadAsync<ProjectionStatus>(projectorId, cancellationToken);
                context.Status = status.Entity;

                if (status.IsNew)
                {
                    _logger.LogInformation("Starting Empty Initialization for Projector {Projector}",
                        context.ProjectorId);

             
                    //start init mode
                    info.Status = "StartingInitialization";
                    await context.StartInitializationModeAsync();

                    // await ReadStreamAsync(context.StreamName, context.Queue, cancellationToken);
                    info.Status = "InitializationStartingStreamReading";
                    await ReadStreamAsync(context.StreamName, context, cancellationToken);

                    //QueueStats stats;
                    //do
                    //{
                    //    await Task.Delay(100, cancellationToken);
                    //    stats = await context.Queue.GetQueueStatsAsync();
                    //} while (stats.Enqueued > stats.Dequeued);

                    info.Status = "InitializationStartingCopy";
                    await projector.CopyEntitiesAsync(context.EntityStore, _entityStore, cancellationToken);

             
                    await context.StopInitializationModeAsync();
                    info.Status = "InitializationFinished";
                }

                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Starting Subscription on EventStore for Projector {Projector}", context.ProjectorId);
                context.StartSubscription(cancellationToken);
                info.Status = "Startet";
            }

        }

        public ProjectionInfo[] Infos => _infos.Values.ToArray();
    }
}
