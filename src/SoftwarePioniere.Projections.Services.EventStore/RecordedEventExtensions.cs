﻿using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using SoftwarePioniere.EventStore;
using SoftwarePioniere.Messaging;

namespace SoftwarePioniere.Projections.Services.EventStore
{
    public static class RecordedEventExtensions
    {
        public static IDomainEvent ToDomainEvent(this RecordedEvent recordedEvent)
        {
            var data = recordedEvent.Data.FromUtf8();
            var meta = recordedEvent.Metadata.FromUtf8();

            var eventHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(meta);

            if (!eventHeaders.ContainsKey(EventStoreConstants.EventShortClrTypeHeader))
                throw new InvalidOperationException("EventShortClrTypeHeader Header not found");

            var typeName = eventHeaders[EventStoreConstants.EventShortClrTypeHeader];


            var eventClrType = Type.GetType(typeName, true);
            var o = JsonConvert.DeserializeObject(data, eventClrType);

            return (IDomainEvent)o;
        }
    }
}
