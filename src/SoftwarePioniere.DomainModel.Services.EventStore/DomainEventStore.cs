using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using Microsoft.Extensions.Logging;
using SoftwarePioniere.DomainModel.Exceptions;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.Messaging;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{
    public class DomainEventStore : IEventStore
    {
        private const int ReadPageSize = 100;

        private static readonly int WritePageSize = 50;
        private readonly Func<Type, string, string> _aggregateIdToStreamName;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly EventStoreConnectionProvider _provider;

        public DomainEventStore(ILoggerFactory loggerFactory, EventStoreConnectionProvider provider) : this(loggerFactory, provider,
            Util.AggregateIdToStreamName) //, Util.EventTypeToEventName)
        {
        }

        public DomainEventStore(ILoggerFactory loggerFactory
            , EventStoreConnectionProvider provider
            , Func<Type, string, string> aggregateIdToStreamName
        )
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger(GetType());
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _aggregateIdToStreamName = aggregateIdToStreamName ??
                                       throw new ArgumentNullException(nameof(aggregateIdToStreamName));
        }

        public Task<bool> CheckAggregateExists<T>(string aggregateId) where T : AggregateRoot
        {
            var streamName = _aggregateIdToStreamName(typeof(T), aggregateId);
            return CheckAggregateExists<T>(aggregateId, streamName);
        }

        public async Task<bool> CheckAggregateExists<T>(string aggregateId, string streamName) where T : AggregateRoot
        {
            _logger.LogDebug("CheckAggregateExists {type} {AggregateId} {StreamName}", typeof(T), aggregateId, streamName);

            //var sliceStart = 1; //Ignores $StreamCreated
            long sliceStart = 0; //Ignores $StreamCreated

            var connection = _provider.Connection;

            var currentSlice = await connection.Value
                .ReadStreamEventsForwardAsync(streamName, sliceStart, 1, false, _provider.OpsCredentials)
                .ConfigureAwait(false);

            if (currentSlice.Status == SliceReadStatus.StreamNotFound)
            {
                _logger.LogTrace("CheckAggregateExists {type} {AggregateId} {StreamName} not found", typeof(T), aggregateId, streamName);
                return false;
            }

            return true;
        }

        public Task<IList<EventDescriptor>> GetEventsForAggregateAsync<T>(string aggregateId) where T : AggregateRoot
        {
            _logger.LogDebug("GetEventsForAggregate {type} {AggregateId}", typeof(T), aggregateId);
            var streamName = _aggregateIdToStreamName(typeof(T), aggregateId);
            return GetEventsForAggregateAsync<T>(aggregateId, int.MaxValue, streamName);
        }

        public Task<IList<EventDescriptor>> GetEventsForAggregateAsync<T>(string aggregateId, string streamName) where T : AggregateRoot
        {
            _logger.LogDebug("GetEventsForAggregate {type} {AggregateId} {StreamName}", typeof(T), aggregateId, streamName);
            return GetEventsForAggregateAsync<T>(aggregateId, int.MaxValue, streamName);
        }

        public async Task SaveEventsAsync<T>(string aggregateId, IEnumerable<IDomainEvent> events, int aggregateVersion)
            where T : AggregateRoot
        {
            _logger.LogDebug("SaveEvents {type} {AggregateId} {AggregateVersion}", typeof(T), aggregateId, aggregateVersion);
            var t = typeof(T);

            var connection = _provider.Connection;

            var streamName = _aggregateIdToStreamName(t, aggregateId);
            var domainEvents = events as IDomainEvent[] ?? events.ToArray();

            //der eventstore fängt bei 0 an, wir im leeren aggregate bei -1 , das erste event erzeugt die version 0, also auch bei 0

            // die version, die das aggregate vor den neuen events hatte
            var originalVersion = aggregateVersion - domainEvents.Length;


            //das ist die nummer, bei der der stream jetzt stehen sollte, so wird sichergestellt, dass in der zwischnzeit keine events geschrieben wurden
            var expectedVersion = originalVersion;
            //var expectedVersion = originalVersion == 0 ? ExpectedVersion.NoStream : originalVersion;

            var eventHeaders = new Dictionary<string, string>
            {
              //  {EventStoreConstants.AggregateNameTypeHeader, t.AssemblyQualifiedName},
             //   {EventStoreConstants.AggregateShortClrTypeHeader, t.GetTypeShortName()},
                {EventStoreConstants.AggregateIdHeader, aggregateId}
            };


            var aggNameAttr = t.GetCustomAttribute<AggregateNameAttribute>();
            if (aggNameAttr != null)
            {
                eventHeaders.Add(EventStoreConstants.AggregateNameHeader, aggNameAttr.Aggregate);
                eventHeaders.Add(EventStoreConstants.BoundedContextNameHeader, aggNameAttr.BoundedContext);
            }

            var eventsToSave = domainEvents.Select(e => e.ToEventData(eventHeaders)).ToList();

            if (eventsToSave.Count < WritePageSize)
            {
                try
                {
                    var result = await connection.Value
                        .AppendToStreamAsync(streamName, expectedVersion, eventsToSave, _provider.OpsCredentials)
                        .ConfigureAwait(false);
                    _logger.LogTrace("EventStore - WriteResult: {@WriteResult}", result);
                }
                catch (WrongExpectedVersionException weException)
                {
                    throw new ConcurrencyException(weException.Message, weException)
                    {
                        ExpectedVersion = aggregateVersion,
                        AggregateType = typeof(T)
                    };
                }
            }
            else
            {
                try
                {
                    var transaction = await connection.Value
                        .StartTransactionAsync(streamName, expectedVersion, _provider.OpsCredentials)
                        .ConfigureAwait(false);

                    var position = 0;
                    while (position < eventsToSave.Count)
                    {
                        var pageEvents = eventsToSave.Skip(position).Take(WritePageSize);
                        await transaction.WriteAsync(pageEvents).ConfigureAwait(false);
                        position += WritePageSize;
                    }

                    await transaction.CommitAsync().ConfigureAwait(false);
                }
                catch (WrongExpectedVersionException weException)
                {
                    throw new ConcurrencyException(weException.Message, weException)
                    {
                        ExpectedVersion = aggregateVersion,
                        AggregateType = typeof(T)
                    };
                }
            }

            //var i = expectedVersion;

            //// iterate through current aggregate events increasing aggregateVersion with each processed event
            //foreach (var @event in domainEvents)
            //{
            //    i++;
            //    eventDescriptors.Add(new EventDescriptor(@event, i));

            //}
        }

        private async Task<IList<EventDescriptor>> GetEventsForAggregateAsync<T>(string aggregateId,
            int aggregateVersion, string streamName)
            where T : AggregateRoot
        {
            _logger.LogDebug("GetEventsForAggregateAsync {type} {AggregateId} {AggregateVersion}", typeof(T), aggregateId, aggregateVersion);

            IList<EventDescriptor> result = new List<EventDescriptor>();

            //   var streamName = _aggregateIdToStreamName(typeof(T), aggregateId);
            //var sliceStart = 1; //Ignores $StreamCreated
            long sliceStart = 0; //Ignores $StreamCreated
            StreamEventsSlice currentSlice;

            var connection = _provider.Connection;

            var i = -1;

            do
            {
                var sliceCount = sliceStart + ReadPageSize <= aggregateVersion
                    ? ReadPageSize
                    : aggregateVersion - sliceStart + 1;

                currentSlice = await connection.Value
                    .ReadStreamEventsForwardAsync(streamName, sliceStart, (int)sliceCount, false,
                        _provider.OpsCredentials).ConfigureAwait(false);

                if (currentSlice.Status == SliceReadStatus.StreamNotFound)
                {
                    throw new AggregateNotFoundException(aggregateId, typeof(T));
                }

                if (currentSlice.Status == SliceReadStatus.StreamDeleted)
                {
                    throw new AggregateNotFoundException(aggregateId, typeof(T));
                }

                sliceStart = currentSlice.NextEventNumber;

                foreach (var evnt in currentSlice.Events)
                {
                    i++;
                    var @event = evnt.OriginalEvent.ToDomainEvent();
                    result.Add(new EventDescriptor(@event, i));
                }

                //    aggregate.ApplyEvent(DeserializeEvent(evnt.OriginalEvent.Metadata, evnt.OriginalEvent.Data));
            } while (aggregateVersion >= currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);

            // if (aggregate.EventVersion != aggregateVersion && aggregateVersion < Int32.MaxValue)
            //     throw new AggregateVersionException(id, typeof(TAggregate), aggregate.EventVersion, aggregateVersion);

            return result;
        }
    }
}
