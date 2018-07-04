using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Foundatio.Caching;
using Foundatio.Queues;
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
            

            Queue = new InMemoryQueue<ProjectionEventData>(new InMemoryQueueOptions<ProjectionEventData>()
            {
                LoggerFactory = loggerFactory
            });
            Queue.StartWorkingAsync(HandleAsync);
        }

        private async Task HandleAsync(IQueueEntry<ProjectionEventData> entry)
        {
            Console.WriteLine($"Handled Item: {entry.Value.EventNumber}");
            CurrentCheckPoint = entry.Value.EventNumber;

            try
            {
                await _projector.HandleAsync(entry.Value.EventData);

                Status.LastCheckPoint = entry.Value.EventNumber;
                
                Status.ModifiedOnUtc = DateTime.UtcNow;                
                await EntityStore.UpdateItemAsync(Status);
                entry.MarkCompleted();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while Processing Event {EventNumber} from {Stream}", entry.Value.EventNumber, StreamName);
                throw;
            }
        }


        public IEntityStore EntityStore
        {
            get
            {
                if (InitializationMode && _initEntityStore != null)
                    return _initEntityStore;

                return _entityStore;
            }
        }

        public IQueue<ProjectionEventData> Queue { get; }
        public ProjectionStatus Status { get; set; }
        public long CurrentCheckPoint { get; private set; }
        public bool IsLiveProcessing { get; private set; }
        public string ProjectorId { get; set; }
        public string StreamName { get; set; }

        public bool InitializationMode { get; private set; }

        private EventStoreStreamCatchUpSubscription _sub;

        public void StartSubscription(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("StartSubscription");

            var cred = _connectionProvider.OpsCredentials;
            var src = _connectionProvider.Connection.Value;

            _sub = src.SubscribeToStreamFrom(StreamName
                , Status.LastCheckPoint
                , CatchUpSubscriptionSettings.Default
                , EventAppeared
                , LiveProcessingStarted
                , SubscriptionDropped
                , cred);

            cancellationToken.Register(_sub.Stop);
        }

        private void SubscriptionDropped(EventStoreCatchUpSubscription sub, SubscriptionDropReason reason, Exception ex)
        {
            _logger.LogError("SubscriptionDropped on StreamId {StreamId}, Reason: {Reason}, Error: {Error}",
                sub.StreamId,
                reason.ToString(), ex?.Message);
        }

        private void LiveProcessingStarted(EventStoreCatchUpSubscription sub)
        {
            _logger.LogDebug("LiveProcessingStarted on StreamId {StreamId}", sub.StreamId);
            IsLiveProcessing = true;
        }

        private async Task EventAppeared(EventStoreCatchUpSubscription sub, ResolvedEvent evt)
        {
            _logger.LogDebug("EventAppeared {SubscriptionName} {Stream}", sub.SubscriptionName, sub.StreamId);
            
            var de = evt.Event.ToDomainEvent();
            var desc = new ProjectionEventData
            {
                EventData = de,
                EventNumber = evt.OriginalEventNumber
            };
            _logger.LogTrace("Enqueue Event @{0}", desc);
            await Queue.EnqueueAsync(desc);
        }

        public async Task StartInitializationModeAsync()
        {
            _logger.LogDebug("StartInitializationMode");

            _initEntityStore = new InMemoryEntityStore(_loggerFactory, NullCacheClient.Instance,
                new InMemoryEntityStoreConnectionProvider());

            InitializationMode = true;
            IsLiveProcessing = false;

            Status = new ProjectionStatus();
            Status.SetEntityId(ProjectorId);

            await _initEntityStore.InsertItemAsync(Status);

        }

        public Task StopInitializationModeAsync()
        {
            _logger.LogDebug("StopInitializationModeAsync");

            InitializationMode = true;
            _initEntityStore = null;

            return Task.CompletedTask;
        }
    }
}