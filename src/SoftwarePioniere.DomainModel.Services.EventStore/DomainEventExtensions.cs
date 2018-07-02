using System;
using System.Collections.Generic;
using System.Globalization;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.Messaging;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{

    public static class DomainEventExtensions
    {

        public static EventData ToEventData(this IDomainEvent evnt, IDictionary<string, string> headers)
        {
            var data = JsonConvert.SerializeObject(evnt).ToUtf8();

            if (headers == null)
                headers = new Dictionary<string, string>();

            var type = evnt.GetType();

            var eventHeaders = new Dictionary<string, string>(headers)
            {
                {  EventStoreConstants.EventClrTypeHeader, type.AssemblyQualifiedName},
                {  EventStoreConstants.EventShortClrTypeHeader, type.GetTypeShortName()},
                {  EventStoreConstants.ServerTimeStampUtcHeader, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}

            };

            var metadata = JsonConvert.SerializeObject(eventHeaders).ToUtf8();

            var eventName = evnt.GetType().GetEventName();

            return new EventData(evnt.Id, eventName, true, data, metadata);
        }

    }
}