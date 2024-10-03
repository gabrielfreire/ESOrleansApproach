using Newtonsoft.Json;
using Orleans;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class EventData
    {
        [Id(0)]
        public byte[] Value { get; set; }
    }

    /// <summary>
    /// Domain Event Class for Database DBSet<>
    /// </summary>
    [GenerateSerializer]
    public class DomainEvent : StateBase
    {
        [Id(0)]
        public Guid TenantId { get; set; }
        [Column(TypeName = "jsonb")]
        [Id(1)]
        public EventData Data { get; set; }
        [Id(2)]
        public string EventType { get; set; }
        [Id(3)]
        public string EventTypeName { get; set; }

        public DomainEvent() { }
        public DomainEvent(Guid tenantId, int version, EventBase @event)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            Version = version;
            Data = new EventData() { Value = Serialize(@event) };
            EventType = @event.GetType().AssemblyQualifiedName;
            EventTypeName = @event.GetType().Name;
        }

        private byte[] Serialize(EventBase @event)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
        }
        public static EventBase Deserialize(string eventType, byte[] data)
        {
            var str = Encoding.UTF8.GetString(data);
            return (EventBase)JsonConvert.DeserializeObject(str, Type.GetType(eventType));
        }
    }
}
