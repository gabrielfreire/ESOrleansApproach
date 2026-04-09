using Newtonsoft.Json;
using Orleans;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Diagnostics;

namespace ESOrleansApproach.Domain.Common
{
    [GenerateSerializer]
    public class EventData
    {
        [Id(0)]
        public byte[] Value { get; set; }

        [Id(1)]
        public List<DomainEventChange> Changes { get; set; } = [];
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
        [Id(4)]
        public string CorrelationId { get; set; }
        [Id(5)]
        public string TraceId { get; set; }
        [Id(6)]
        public string RequestPath { get; set; }
        [Id(7)]
        public string Source { get; set; }
        [Id(8)]
        public string UserAgent { get; set; }

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

        public void ApplyRequestContext(HttpContextSurrogate httpContext)
        {
            if (httpContext is null)
                return;

            RequestPath ??= httpContext.RequestPath;
            UserAgent ??= httpContext.UserAgent;
            Source ??= httpContext.Host;
        }

        public void ApplyActivityContext(Activity activity)
        {
            if (activity is null)
                return;

            TraceId ??= activity.TraceId.ToString();

            if (string.IsNullOrWhiteSpace(CorrelationId))
            {
                CorrelationId = activity.ParentId ?? activity.TraceId.ToString();
            }
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
