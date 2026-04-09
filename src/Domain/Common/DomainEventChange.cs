using Orleans;
using System;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class DomainEventChange
    {
        [Id(0)]
        public string EntityType { get; set; }

        [Id(1)]
        public Guid EntityId { get; set; }

        [Id(2)]
        public int ChangesCount { get; set; }

        [Id(3)]
        public string FieldName { get; set; }

        [Id(4)]
        public object PreviousValue { get; set; }

        [Id(5)]
        public object CurrentValue { get; set; }

        [Id(6)]
        public string Action { get; set; }

        [Id(7)]
        public DateTimeOffset Timestamp { get; set; }

        [Id(8)]
        public string Entity { get; set; }
    }
}