using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.ReadModel;
using SoftwarePioniere.ReadModel.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public class EventStoreProjectionContext : IProjectionContext
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly EventStoreConnectionProvider _connectionProvider;
        private readonly IEntityStore _entityStore;
        private readonly IReadModelProjector _projector;
        private readonly ILogger _logger;
        private InMemoryEntityStore _initEntityStore;

        public EventStoreProjectionContext(ILoggerFactory loggerFactory
            , EventStoreConnectionProvider connectionProvider
            , IEntityStore entityStore
            , IReadModelProjector projector
        )
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger(GetType());
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _entityStore = entityStore ?? throw new ArgumentNullException(nameof(entityStore));
            _projector = projector ?? throw new ArgumentNullException(nameof(projector));

            //Queue = new InMemoryQueue<ProjectionEventData>(new InMemoryQueueOptions<ProjectionEventData>()
            //{
            //    LoggerFactory = loggerFactory
            //});
            //Queue.StartWorkingAsync(HandleAsync);
        }

        internal async Task HandleEventAsync(ProjectionEventData entry)
        {
            _logger.LogTrace("Handled Item {EventNumber} {StreamName} {ProjectorId}", entry.EventNumber, StreamName, ProjectorId);
            CurrentCheckPoint = entry.EventNumber;

            try
            {
                await _projector.ProcessEventAsync(entry.EventData);
                Status.LastCheckPoint = entry.EventNumber;
                Status.ModifiedOnUtc = DateTime.UtcNow;
                await EntityStore.UpdateItemAsync(Status, _cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while Processing Event {EventNumber} from {StreamName} {ProjectorId}", entry.EventNumber, StreamName, ProjectorId);
                throw;
            }
        }

        //private async Task HandleAsync(IQueueEntry<ProjectionEventData> entry)
        //{
        //    _logger.LogDebug("Handled Item {EventNumber}", entry.Value.EventNumber);

        //    try
        //    {
        //        await HandleEventAsync(entry.Value);
        //        entry.MarkCompleted();
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.LogError(e, "Error while Processing Event {EventNumber} from {Stream} {ProjectorId}", entry.Value.EventNumber, StreamName, ProjectorId);
        //        throw;
        //    }
        //}


        public IEntityStore EntityStore
        {
            get
            {
                if (InitializationMode && _initEntityStore != null)
                    return _initEntityStore;

                return _entityStore;
            }
        }

        //public IQueue<ProjectionEventData> Queue { get; }
        public ProjectionStatus Status { get; set; }
        public long CurrentCheckPoint { get; private set; }
        public bool IsLiveProcessing { get; private set; }
        public string ProjectorId { get; set; }
        public string StreamName { get; set; }
        public bool IsReady { get; set; }

        public bool InitializationMode { get; private set; }

        // private EventStoreStreamCatchUpSubscription _sub;


        private CancellationToken _cancellationToken;

        public Task StartSubscription(CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.LogInformation("StartSubscription for Projector {ProjectorId} on {Stream}", ProjectorId, StreamName);
            _cancellationToken = cancellationToken;
            return StartSubscriptionInternal();
        }

        private async Task StartSubscriptionInternal()
        {
            _logger.LogDebug("StartSubscriptionInternal for Projector {ProjectorId} on {Stream}", ProjectorId, StreamName);

            var cred = _connectionProvider.OpsCredentials;
            var src = await _connectionProvider.GetActiveConnection();
            long? lastCheckpoint = null;

            if (Status.LastCheckPoint.HasValue && Status.LastCheckPoint != -1)
            {
                lastCheckpoint = Status.LastCheckPoint;
            }

            var sub = src.SubscribeToStreamFrom(StreamName
                , lastCheckpoint
                , CatchUpSubscriptionSettings.Default
                , EventAppeared
                , LiveProcessingStarted
                , SubscriptionDropped
                , cred);

            _cancellationToken.Register(sub.Stop);
        }


        private async void SubscriptionDropped(EventStoreCatchUpSubscription sub, SubscriptionDropReason reason, Exception ex)
        {
            _logger.LogError(ex, "SubscriptionDropped on StreamId {StreamId}, Projector {ProjectorId}, Reason: {Reason}",
                sub.StreamId,
                ProjectorId,
                reason.ToString());
            sub.Stop();

            if (!_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Re Subscribe Subscription");
                await StartSubscriptionInternal();
            }
        }

        private void LiveProcessingStarted(EventStoreCatchUpSubscription sub)
        {
            _logger.LogDebug("LiveProcessingStarted on StreamId {StreamId}, Projector {ProjectorId}", sub.StreamId, ProjectorId);
            IsLiveProcessing = true;
        }

        private async Task EventAppeared(EventStoreCatchUpSubscription sub, ResolvedEvent evt)
        {
            _logger.LogTrace("EventAppeared {SubscriptionName} {Stream} Projector {ProjectorId}", sub.SubscriptionName, sub.StreamId, ProjectorId);

            var de = evt.Event.ToDomainEvent();
            var desc = new ProjectionEventData
            {
                EventData = de,
                EventNumber = evt.OriginalEventNumber
            };
            //_logger.LogTrace("Enqueue Event @{0}", desc);
            // await Queue.EnqueueAsync(desc);
            await HandleEventAsync(desc);
        }

        public async Task StartInitializationModeAsync()
        {
            _logger.LogDebug("StartInitializationMode");

            _initEntityStore = new InMemoryEntityStore(new InMemoryEntityStoreOptions
            {
                CachingDisabled = true,
                CacheClient = NullCacheClient.Instance,
                LoggerFactory = _loggerFactory
            }, new InMemoryEntityStoreConnectionProvider());

            InitializationMode = true;
            IsLiveProcessing = false;
            IsReady = false;

            Status = new ProjectionStatus();
            Status.SetEntityId(ProjectorId);
            Status.LastCheckPoint = -1;

            await _initEntityStore.InsertItemAsync(Status);

        }

        public Task StopInitializationModeAsync()
        {
            _logger.LogDebug("StopInitializationModeAsync");

            InitializationMode = true;
            IsReady = true;
            _initEntityStore = null;
            
            return Task.CompletedTask;
        }
    }
}