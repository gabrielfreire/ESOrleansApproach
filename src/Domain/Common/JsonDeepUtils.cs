using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ESOrleansApproach.Domain.Common
{
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(Tenant))]
    [JsonSerializable(typeof(Customer))]
    [JsonSerializable(typeof(ShoppingCart))]
    [JsonSerializable(typeof(ShoppingCartItem))]
    [JsonSerializable(typeof(StateBase))]
    [JsonSerializable(typeof(JObject))]
    [JsonSerializable(typeof(EventBase))]
    [JsonSerializable(typeof(DomainEvent))]
    [JsonSerializable(typeof(EventData))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(DomainEventChange))]
    [JsonSerializable(typeof(object))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
    public static class JsonDeepUtils
    {
        // FrozenDictionary provides faster lookups than Dictionary for read-only collections
        private static readonly FrozenDictionary<Type, JsonTypeInfo> _typeMap = new Dictionary<Type, JsonTypeInfo>
        {
            { typeof(StateBase), JsonContext.Default.StateBase },
            { typeof(Tenant), JsonContext.Default.Tenant },
            { typeof(Customer), JsonContext.Default.Customer },
            { typeof(ShoppingCart), JsonContext.Default.ShoppingCart },
            { typeof(DomainEvent), JsonContext.Default.DomainEvent },
            { typeof(EventData), JsonContext.Default.EventData },
            { typeof(JsonElement), JsonContext.Default.JsonElement },
            { typeof(DomainEventChange), JsonContext.Default.DomainEventChange },
            { typeof(object), JsonContext.Default.Object }
        }.ToFrozenDictionary();

        /// <summary>
        /// Thread-static reusable buffer for serialization. Each thread gets its own
        /// instance so no locking is needed; Reset() clears the written count without
        /// releasing the underlying array, so subsequent calls reuse the same memory.
        /// </summary>
        [ThreadStatic]
        private static ArrayBufferWriter<byte>? t_buffer;

        /// <summary>Second thread-static buffer used only by DeepEquals to avoid
        /// overwriting the primary buffer when comparing two objects.</summary>
        [ThreadStatic]
        private static ArrayBufferWriter<byte>? t_buffer2;

        private static ArrayBufferWriter<byte> RentBuffer(ref ArrayBufferWriter<byte>? slot)
        {
            var buf = slot ??= new ArrayBufferWriter<byte>(256);
            buf.ResetWrittenCount();
            return buf;
        }

        private static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public static JsonTypeInfo? GetJsonTypeInfo(Type type)
            => _typeMap.TryGetValue(type, out var info) ? info : null;

        public static bool DeepEquals(object obj1, object obj2)
        {
            if (ReferenceEquals(obj1, obj2)) return true;
            if (obj1 is null || obj2 is null) return false;

            var type = obj1.GetType();
            var jsonTypeInfo = GetJsonTypeInfo(type)
                ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {type}");

            // Serialize both objects into reusable thread-static buffers — no byte[] allocation
            var buf1 = RentBuffer(ref t_buffer);
            using var writer1 = new Utf8JsonWriter(buf1);
            System.Text.Json.JsonSerializer.Serialize(writer1, obj1, jsonTypeInfo);

            var buf2 = RentBuffer(ref t_buffer2);
            using var writer2 = new Utf8JsonWriter(buf2);
            System.Text.Json.JsonSerializer.Serialize(writer2, obj2, jsonTypeInfo);

            return buf1.WrittenSpan.SequenceEqual(buf2.WrittenSpan);
        }

        /// <summary>
        /// Deep-clones an object by serializing into a thread-static pooled buffer
        /// and deserializing from the written span. No per-call byte[] allocations.
        /// </summary>
        public static object? DeepClone(object obj)
        {
            if (obj is null) return null;

            var type = obj.GetType();
            var jsonTypeInfo = GetJsonTypeInfo(type)
                ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {type}");

            var buf = RentBuffer(ref t_buffer);
            using var writer = new Utf8JsonWriter(buf);
            System.Text.Json.JsonSerializer.Serialize(writer, obj, jsonTypeInfo);

            return System.Text.Json.JsonSerializer.Deserialize(buf.WrittenSpan, type, jsonTypeInfo.Options);
        }

        public static byte[] Serialize(object obj)
        {
            if (obj is null) return null;

            var type = obj.GetType();
            var jsonTypeInfo = GetJsonTypeInfo(type)
                ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {type}");

            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj, jsonTypeInfo);
        }

        public static string SerializeToString(object obj)
        {
            if (obj is null) return null;

            var type = obj.GetType();
            var jsonTypeInfo = GetJsonTypeInfo(type)
                ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {type}");

            var buf = RentBuffer(ref t_buffer);
            using var writer = new Utf8JsonWriter(buf);
            System.Text.Json.JsonSerializer.Serialize(writer, obj, jsonTypeInfo);
            return Encoding.UTF8.GetString(buf.WrittenSpan);
        }

        public static object Deserialize(byte[] bytes, Type type)
        {
            if (bytes is null) return null;

            var jsonTypeInfo = GetJsonTypeInfo(type)
                ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {type}");

            return System.Text.Json.JsonSerializer.Deserialize(bytes, type, jsonTypeInfo.Options);
        }
    }
}