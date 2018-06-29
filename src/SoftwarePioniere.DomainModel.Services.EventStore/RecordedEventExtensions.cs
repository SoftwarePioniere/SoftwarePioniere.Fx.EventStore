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
            var data = recordedEvent.Data.GetToStringFromEncoded();
            var meta = recordedEvent.Metadata.GetToStringFromEncoded();

            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventShortClrTypeHeader))
                throw new InvalidOperationException("EventShortClrTypeHeader Header not found");

            var typeName = eventHeaders[EventStoreConstants.EventShortClrTypeHeader];




            var eventClrType = Type.GetType(typeName, true);
            var o = JsonConvert.DeserializeObject(data, eventClrType);

            return (IDomainEvent)o;
        }



        public static T ToDomainEvent<T>(this RecordedEvent recordedEvent)
        {
            var meta = recordedEvent.Metadata.GetToStringFromEncoded();
            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventShortClrTypeHeader))
                throw new InvalidOperationException("EventShortClrTypeHeader Header not found");

            if (!string.Equals(eventHeaders[EventStoreConstants.EventShortClrTypeHeader], typeof(T).GetTypeShortName()))
                throw new InvalidOperationException("Event is not compatible");

            var data = recordedEvent.GetJsonData();

            return JsonConvert.DeserializeObject<T>(data);
        }

        public static string GetJsonData(this RecordedEvent recordedEvent)
        {
            var data = recordedEvent.Data.GetToStringFromEncoded();
            return data;

        }


        public static Tuple<T, string> ToDomainEventWithJson<T>(this RecordedEvent recordedEvent)
        {
            var meta = recordedEvent.Metadata.GetToStringFromEncoded();
            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventShortClrTypeHeader))
                throw new InvalidOperationException("EventShortClrTypeHeader Header not found");

            if (!string.Equals(eventHeaders[EventStoreConstants.EventShortClrTypeHeader], (typeof(T).GetTypeShortName())))
                throw new InvalidOperationException("Event is not compatible");

            var data = recordedEvent.Data.GetToStringFromEncoded();
            return new Tuple<T, string>(JsonConvert.DeserializeObject<T>(data), data);
        }


    }
}