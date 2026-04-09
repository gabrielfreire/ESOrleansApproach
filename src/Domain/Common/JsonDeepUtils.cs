using System;
using System.Text;
using System.Text.Json;

namespace ESOrleansApproach.Domain.Common
{
    public static class JsonDeepUtils
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public static bool DeepEquals(object obj1, object obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;

            if (obj1 is null || obj2 is null)
                return false;

            return SerializeToString(obj1) == SerializeToString(obj2);
        }

        public static object DeepClone(object obj)
        {
            if (obj is null)
                return null;

            var type = obj.GetType();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, type, Options);
            return JsonSerializer.Deserialize(bytes, type, Options);
        }

        public static byte[] Serialize(object obj)
        {
            if (obj is null)
                return null;

            return JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType(), Options);
        }

        public static string SerializeToString(object obj)
        {
            if (obj is null)
                return null;

            return Encoding.UTF8.GetString(Serialize(obj));
        }

        public static object Deserialize(byte[] bytes, Type type)
        {
            if (bytes is null)
                return null;

            return JsonSerializer.Deserialize(bytes, type, Options);
        }
    }
}