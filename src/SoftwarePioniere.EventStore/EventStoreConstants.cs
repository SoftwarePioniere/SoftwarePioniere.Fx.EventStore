namespace SoftwarePioniere.EventStore
{
    public static class EventStoreConstants
    {

        public const string BoundedContextNameHeader = "BoundedContextName";
        public const string AggregateNameHeader = "AggregateName";
        
        public const string AggregateShortClrTypeHeader = "AggregateShortClrType";
        public const string AggregateNameTypeHeader = "AggregateNameType";
        public const string AggregateIdHeader = "AggregateId";

        // private const string CommitIdHeader = "CommitId";
        public const string EventClrTypeHeader = "EventClrType";
        public const string EventShortClrTypeHeader = "EventShortClrType";
        public const string ServerTimeStampUtcHeader = "ServerTimeStampUtc";

    }
}