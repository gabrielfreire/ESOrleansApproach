using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class EventBase
    {
        [Id(0)]
        public Guid Id { get; set; }
        [Id(1)]
        public DateTimeOffset Timestamp { get; private set; }
        [Id(2)]
        public string Aggregate { get; set; }
        [Id(3)]
        public Guid AggregateId { get; set; }
        [Id(4)]
        public string Type { get; private set; }
        [Id(5)]
        public int Version { get; set; } = -1;
        protected EventBase()
        {
            Timestamp = DateTimeOffset.UtcNow;
        }

        public EventBase(string aggregate) : this()
        {
            Id = Guid.NewGuid();
            Type = $"{aggregate}_{GetType().Name}";
        }

        public EventBase(string aggregate, Guid aggregateId) : this(aggregate)
        {
            Id = Guid.NewGuid();
            Type = $"{aggregate}_{GetType().Name}";
            AggregateId = aggregateId;
        }
    }
}
