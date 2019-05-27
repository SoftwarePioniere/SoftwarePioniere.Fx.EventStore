using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SoftwarePioniere.DomainModel.Subscriptions;
using SoftwarePioniere.EventStore;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SoftwarePioniere.DomainModel.Services.EventStore.Subscriptions
{

    public class PersistentSubscriptionAdapter<T> : IPersistentSubscriptionAdapter<T>
    {
        private readonly EventStoreConnectionProvider _connectionProvider;
        private readonly CancellationToken _cancellationToken;
    
        public PersistentSubscriptionAdapter(EventStoreConnectionProvider connectionProvider, IApplicationLifetime applicationLifetime)
        {
            if (applicationLifetime == null)
            {
                throw new ArgumentNullException(nameof(applicationLifetime));
            }

            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _cancellationToken = applicationLifetime.ApplicationStopping;
        }

        private string _stream;
        string _groupName;
        private ILogger _logger;

        private int _bufferSize;
        private Func<T, IDictionary<string, string>, Task> _eventAppeared;
        private bool _skipRemoved;

        public Task ConnectToPersistentSubscription(string stream,
            string groupName, ILogger logger
            , Func<T, IDictionary<string, string>, Task> eventAppeared,
            int bufferSize = 10, bool skipRemoved = true)
        {
            _skipRemoved = skipRemoved;
            _eventAppeared = eventAppeared;
            _bufferSize = bufferSize;

            _stream = stream;
            _groupName = groupName;
            _logger = logger;


            return ConnectToPersistentSubscriptionInternal();
        }

        private async Task ConnectToPersistentSubscriptionInternal()
        {
            var cred = _connectionProvider.OpsCredentials;
            var con = await _connectionProvider.GetActiveConnection();

            await con.ConnectToPersistentSubscriptionAsync(_stream, _groupName, EventAppeared, SubscriptionDropped, cred, _bufferSize);
        }

        private async void SubscriptionDropped(EventStorePersistentSubscriptionBase sub, SubscriptionDropReason reason, Exception ex)
        {
            _logger.LogError(ex, "PersistentSubscription Dropped on StreamId {StreamId}, Reason: {Reason}", sub, reason.ToString());

            if (!_cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Re Subscribe PersistentSubscription ");
                await ConnectToPersistentSubscriptionInternal();
            }
        }


        private async Task EventAppeared(EventStorePersistentSubscriptionBase sub, ResolvedEvent subEvent)
        {
            var con = await _connectionProvider.GetActiveConnection();

            //     var origEvent = await con.ReadEventAsync(subEvent.OriginalStreamId, subEvent.OriginalEventNumber, true, _connectionProvider.OpsCredentials);
            
            var subData = subEvent.Event.Data.FromUtf8();
            var xx = subData.Split('@');
            if (xx.Length == 2)
            {
                var eventNumber = int.Parse(xx[0]);
                var stream = xx[1];

                var eosEvents = await con.ReadStreamEventsBackwardAsync(stream,
                    StreamPosition.End,
                    1,
                    false,
                    _connectionProvider.OpsCredentials);

                ResolvedEvent? @event = null;
                bool isResultRemoved = false;
                bool eventIsRemovedEvent = false;

                if (eosEvents.Events.Length > 0)
                {
                    var eosEvent = eosEvents.Events[0];
                    if (eosEvent.OriginalEventNumber == eventNumber)
                    {
                        _logger.LogDebug("Event From Subscription is Last Event in Stream");

                        if (eosEvent.Event.EventType == "ResultRemoved")
                        {
                            _logger.LogDebug("The current event is the removed event");
                            eventIsRemovedEvent = true;

                        }
                    }
                    else
                    {
                        _logger.LogDebug("Checking Last Event if Removed");
                        if (eosEvent.Event.EventType == "ResultRemoved")
                        {
                            _logger.LogDebug("Last Event Is Removed");
                            isResultRemoved = true;
                        }
                    }
                }

                if (!eventIsRemovedEvent && (!isResultRemoved || !_skipRemoved))
                {
                    var curEvent = await con.ReadEventAsync(stream, eventNumber, false, _connectionProvider.OpsCredentials);
                    if (curEvent.Event.HasValue)
                    {
                        @event = curEvent.Event;
                    }
                }

                //

                if (@event.HasValue)
                {
                    var data = @event.Value.Event.Data.FromUtf8();

                    var item = JsonConvert.DeserializeObject<T>(data);

                    var parentState = new Dictionary<string, string>();
                    await _eventAppeared(item, parentState);
                    

                    //await _telemetryAdapter.RunDependencyAsync("PERS SUBS", "EVENTSTORE", state => _eventAppeared(item, state), parentState, _logger);

                    sub.Acknowledge(subEvent);
                }
                else
                {
                    _logger.LogDebug("Event Has no Value");
                }

            }
        }
    }
}