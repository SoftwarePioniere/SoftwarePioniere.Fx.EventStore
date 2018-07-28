using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Foundatio.Queues;
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
        }


        private async Task<bool> ReadStreamAsync(string stream, IQueue<ProjectionEventData> queue, CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogDebug("ReadFromStreamAsync {Stream}", stream);

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

                    var de = ev.Event.ToDomainEvent();
                    var desc = new ProjectionEventData
                    {
                        EventData = de,
                        EventNumber = ev.OriginalEventNumber
                    };

                    _logger.LogTrace("Enqueue Event @{0}", desc);
                    await queue.EnqueueAsync(desc);

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
                await _connectionProvider.Connection.Value.AppendToStreamAsync(name, -1, events, _connectionProvider.OpsCredentials).ConfigureAwait(false);

            }
            else
            {
                _logger.LogDebug("Stream is not Empty {StreamName}", streamName);
            }

        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogInformation("InitializeAsync");

            foreach (var projector in _projectors)
            {
                var projectorId = projector.GetType().FullName;

                _logger.LogDebug("Preparing Stream for Projector {Projector}", projector.GetType().Name);
                await InsertEmptyDomainEventIfStreamIsEmpty(projector.StreamName);

                var context = new EventStoreProjectionContext(_loggerFactory, _connectionProvider, _entityStore, projector)
                {
                    StreamName = projector.StreamName,
                    ProjectorId = projectorId
                };

                projector.Context = context;

                //try load statuc
                var status = await _entityStore.LoadAsync<ProjectionStatus>(projectorId);
                context.Status = status.Entity;

                if (status.IsNew)
                {
                    //start init mode
                    await context.StartInitializationModeAsync();
                    ReadStreamAsync(context.StreamName, context.Queue, cancellationToken).Wait(cancellationToken);

                    QueueStats stats;

                    do
                    {
                        await Task.Delay(100, cancellationToken);
                        stats = await context.Queue.GetQueueStatsAsync();
                    } while (stats.Enqueued > stats.Dequeued);

                    await projector.CopyEntitiesAsync(context.EntityStore, _entityStore, cancellationToken);

                    await context.StopInitializationModeAsync();
                }

                context.StartSubscription(cancellationToken);

            }

        }
    }
}
