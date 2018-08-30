using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.Messaging;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{
    public static class RecordedEventExtensions
    {

        public static IDomainEvent ToDomainEvent(this RecordedEvent recordedEvent)
        {
            var data = recordedEvent.Data.FromUtf8();
            var meta = recordedEvent.Metadata.FromUtf8();

            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventTypeHeader))
                throw new InvalidOperationException("EventTypeHeader Header not found");

            var typeName = eventHeaders[EventStoreConstants.EventTypeHeader];

            var eventClrType = Type.GetType(typeName, true);
            var o = JsonConvert.DeserializeObject(data, eventClrType);

            return o as IDomainEvent;
        }

        public static T ToEvent<T>(this RecordedEvent recordedEvent)
        {
            var meta = recordedEvent.Metadata.FromUtf8();
            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventTypeHeader))
                throw new InvalidOperationException("EventTypeHeader Header not found");

            if (!string.Equals(eventHeaders[EventStoreConstants.EventTypeHeader], typeof(T).GetTypeShortName()))
                throw new InvalidOperationException("Event is not compatible");

            var data = recordedEvent.GetJsonData();

            return JsonConvert.DeserializeObject<T>(data);
        }

        public static string GetJsonData(this RecordedEvent recordedEvent)
        {
            var data = recordedEvent.Data.FromUtf8();
            return data;

        }

        public static Tuple<T, string> ToEventWithJson<T>(this RecordedEvent recordedEvent)
        {
            var meta = recordedEvent.Metadata.FromUtf8();
            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventTypeHeader))
                throw new InvalidOperationException("EventTypeHeader Header not found");

            if (!string.Equals(eventHeaders[EventStoreConstants.EventTypeHeader], (typeof(T).GetTypeShortName())))
                throw new InvalidOperationException("Event is not compatible");

            var data = recordedEvent.Data.FromUtf8();
            return new Tuple<T, string>(JsonConvert.DeserializeObject<T>(data), data);
        }


    }
}